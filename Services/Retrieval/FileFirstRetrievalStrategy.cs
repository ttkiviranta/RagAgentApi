using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RagAgentApi.Data;

namespace RagAgentApi.Services.Retrieval;

/// <summary>
/// FileFirst retrieval strategy.
/// Prioritizes loading original files from Azure Blob Storage.
/// Falls back to database content if blobs are unavailable.
/// Best for small document collections where complete context is beneficial.
/// </summary>
public class FileFirstRetrievalStrategy : IRetrievalStrategy
{
    private readonly RagDbContext _dbContext;
    private readonly IAzureOpenAIService _openAIService;
    private readonly IBlobStorageService? _blobStorageService;
    private readonly ILogger<FileFirstRetrievalStrategy> _logger;
    private readonly RetrievalSettings _settings;

    public string Name => "FileFirst";

    public FileFirstRetrievalStrategy(
        RagDbContext dbContext,
        IAzureOpenAIService openAIService,
        IOptions<RetrievalSettings> settings,
        ILogger<FileFirstRetrievalStrategy> logger,
        IBlobStorageService? blobStorageService = null)
    {
        _dbContext = dbContext;
        _openAIService = openAIService;
        _settings = settings.Value;
        _logger = logger;
        _blobStorageService = blobStorageService;
    }

    public async Task<RetrievalResult> ExecuteAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new RetrievalResult
        {
            StrategyUsed = Name,
            ConfiguredMode = "FileFirst"
        };

        try
        {
            _logger.LogInformation("[FileFirstStrategy] Loading documents for query: '{Query}'", query);

            // Load all documents with their chunks
            var documents = await _dbContext.Documents
                .Include(d => d.Chunks)
                .Where(d => d.Status == "active" || d.Status == "completed")
                .OrderByDescending(d => d.ScrapedAt)
                .Take(topK * 2)
                .ToListAsync(cancellationToken);

            if (!documents.Any())
            {
                _logger.LogWarning("[FileFirstStrategy] No documents found in database");
                result.Answer = "Tietokannasta ei löytynyt dokumentteja.";
                result.Success = true;
                result.Metadata["no_results"] = true;
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return result;
            }

            // Try to load content from Blob Storage first
            var allContent = new List<string>();
            var blobsUsed = 0;
            var fallbackUsed = 0;

            foreach (var doc in documents)
            {
                var content = await GetDocumentContentAsync(doc, cancellationToken);
                if (!string.IsNullOrEmpty(content))
                {
                    // Add document header for context
                    var header = $"=== Document: {doc.Title ?? doc.OriginalFileName ?? doc.Url} ===";
                    allContent.Add($"{header}\n{content}");

                    if (doc.HasOriginalFile && _blobStorageService?.IsEnabled == true)
                        blobsUsed++;
                    else
                        fallbackUsed++;
                }
            }

            _logger.LogInformation("[FileFirstStrategy] Loaded content: {BlobCount} from Blob Storage, {FallbackCount} from database",
                blobsUsed, fallbackUsed);

            result.Metadata["blobs_used"] = blobsUsed;
            result.Metadata["database_fallback_used"] = fallbackUsed;

            if (!allContent.Any())
            {
                result.Answer = "Dokumenteista ei voitu lukea sisältöä.";
                result.Success = true;
                result.Metadata["no_content"] = true;
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return result;
            }

            // Build context and truncate if needed
            var context = string.Join("\n\n", allContent);
            const int maxContextLength = 30000; // ~7500 tokens
            if (context.Length > maxContextLength)
            {
                context = context.Substring(0, maxContextLength) + "\n\n[Content truncated...]";
                _logger.LogInformation("[FileFirstStrategy] Context truncated to {Length} characters", maxContextLength);
            }

            var systemPrompt = @"You are a helpful AI assistant that answers questions based on the provided documents.

IMPORTANT RULES:
- Answer using information from the documents below
- If the documents don't contain relevant information, state that clearly
- Be concise and cite specific documents when relevant

Documents:
" + context;

            var answer = await _openAIService.GetChatCompletionAsync(systemPrompt, query, cancellationToken);

            // Add indicator based on content source
            var sourceIndicator = blobsUsed > 0 
                ? "📁 Vastaus alkuperäisten tiedostojen perusteella" 
                : "📄 Vastaus tallennetun sisällön perusteella";
            result.Answer = $"{sourceIndicator}:\n\n{answer}";

            result.Sources = documents.Take(topK).Select(d => new RetrievalSource
            {
                Url = d.BlobUri ?? d.Url ?? "",
                Title = d.Title ?? d.OriginalFileName,
                Content = (d.Content?.Length ?? 0) > 200 
                    ? d.Content!.Substring(0, 200) + "..." 
                    : d.Content ?? "",
                RelevanceScore = 1.0,
                Metadata = new Dictionary<string, object>
                {
                    { "has_blob", d.HasOriginalFile },
                    { "file_size_bytes", d.OriginalFileSizeBytes ?? 0 },
                    { "mime_type", d.MimeType ?? "unknown" }
                }
            }).ToList();

            result.Success = true;
            result.Metadata["documents_used"] = documents.Count;
            result.Metadata["total_chunks"] = documents.Sum(d => d.Chunks.Count);

            _logger.LogInformation("[FileFirstStrategy] Generated answer using {DocCount} documents ({BlobCount} from blob)", 
                documents.Count, blobsUsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileFirstStrategy] Error during retrieval");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        stopwatch.Stop();
        result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    /// <summary>
    /// Get document content, prioritizing Blob Storage, with fallback to database
    /// </summary>
    private async Task<string> GetDocumentContentAsync(
        RagAgentApi.Models.PostgreSQL.Document document, 
        CancellationToken cancellationToken)
    {
        // Try Blob Storage first if available
        if (document.HasOriginalFile && _blobStorageService?.IsEnabled == true)
        {
            try
            {
                _logger.LogDebug("[FileFirstStrategy] Attempting to load from Blob: {BlobUri}", document.BlobUri);

                var blobResult = await _blobStorageService.DownloadFileAsync(document.BlobUri!, cancellationToken);

                if (blobResult.Success && blobResult.Content != null)
                {
                    // Extract text from blob content based on MIME type
                    var text = await ExtractTextFromBlobAsync(blobResult, document.MimeType, cancellationToken);
                    if (!string.IsNullOrEmpty(text))
                    {
                        _logger.LogDebug("[FileFirstStrategy] Successfully loaded {Bytes} bytes from Blob", blobResult.Content.Length);
                        return text;
                    }
                }

                _logger.LogWarning("[FileFirstStrategy] Blob download failed for {BlobUri}, falling back to database", document.BlobUri);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FileFirstStrategy] Error loading from Blob: {BlobUri}, falling back to database", document.BlobUri);
            }
        }

        // Fallback: Use chunks or content from database
        if (document.Chunks.Any())
        {
            return string.Join("\n\n", document.Chunks.OrderBy(c => c.ChunkIndex).Select(c => c.Content));
        }

        return document.Content ?? string.Empty;
    }

    /// <summary>
    /// Extract text from blob content based on MIME type
    /// </summary>
    private async Task<string> ExtractTextFromBlobAsync(
        BlobDownloadResult blobResult, 
        string? mimeType, 
        CancellationToken cancellationToken)
    {
        if (blobResult.Content == null || blobResult.Content.Length == 0)
            return string.Empty;

        var contentType = mimeType ?? blobResult.ContentType ?? "application/octet-stream";

        try
        {
            // Handle text files
            if (contentType.StartsWith("text/") || 
                contentType == "application/json" ||
                contentType == "application/xml")
            {
                return System.Text.Encoding.UTF8.GetString(blobResult.Content);
            }

            // Handle PDF files
            if (contentType == "application/pdf")
            {
                return ExtractTextFromPdf(blobResult.Content);
            }

            // Default: try to read as UTF-8 text
            return System.Text.Encoding.UTF8.GetString(blobResult.Content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileFirstStrategy] Failed to extract text from blob ({ContentType})", contentType);
            return string.Empty;
        }
    }

    /// <summary>
    /// Extract text from PDF bytes using iText
    /// </summary>
    private string ExtractTextFromPdf(byte[] pdfBytes)
    {
        try
        {
            using var pdfDoc = new iText.Kernel.Pdf.PdfDocument(
                new iText.Kernel.Pdf.PdfReader(new MemoryStream(pdfBytes)));

            var text = new System.Text.StringBuilder();
            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                var page = pdfDoc.GetPage(i);
                var pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page);
                text.AppendLine(pageText);
            }
            return text.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileFirstStrategy] PDF extraction failed");
            return string.Empty;
        }
    }
}
