using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RagAgentApi.Data;

namespace RagAgentApi.Services.Retrieval;

/// <summary>
/// FileFirst retrieval strategy.
/// Loads all documents directly from the database and uses them as context.
/// Best for small document collections where complete context is beneficial.
/// </summary>
public class FileFirstRetrievalStrategy : IRetrievalStrategy
{
    private readonly RagDbContext _dbContext;
    private readonly IAzureOpenAIService _openAIService;
    private readonly ILogger<FileFirstRetrievalStrategy> _logger;
    private readonly RetrievalSettings _settings;

    public string Name => "FileFirst";

    public FileFirstRetrievalStrategy(
        RagDbContext dbContext,
        IAzureOpenAIService openAIService,
        IOptions<RetrievalSettings> settings,
        ILogger<FileFirstRetrievalStrategy> logger)
    {
        _dbContext = dbContext;
        _openAIService = openAIService;
        _settings = settings.Value;
        _logger = logger;
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
            _logger.LogInformation("[FileFirstStrategy] Loading all documents for query: '{Query}'", query);

            // Load all documents with their chunks
            var documents = await _dbContext.Documents
                .Include(d => d.Chunks)
                .Where(d => d.Status == "active" || d.Status == "completed")
                .OrderByDescending(d => d.ScrapedAt)
                .Take(topK * 2) // Take more documents since we're not filtering by relevance
                .ToListAsync(cancellationToken);

            if (!documents.Any())
            {
                _logger.LogWarning("[FileFirstStrategy] No documents found in database");
                result.Answer = "Tietokannasta ei löytynyt dokumentteja.";
                result.Success = true;
                result.Metadata["no_results"] = true;
            }
            else
            {
                // Build context from all document chunks
                var allContent = new List<string>();
                foreach (var doc in documents)
                {
                    if (doc.Chunks.Any())
                    {
                        allContent.AddRange(doc.Chunks.OrderBy(c => c.ChunkIndex).Select(c => c.Content));
                    }
                    else if (!string.IsNullOrEmpty(doc.Content))
                    {
                        allContent.Add(doc.Content);
                    }
                }

                // Truncate if too long (approximate token limit)
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
                result.Answer = "📁 Vastaus tiedostojen perusteella:\n\n" + answer;

                result.Sources = documents.Take(topK).Select(d => new RetrievalSource
                {
                    Url = d.Url ?? "",
                    Title = d.Title,
                    Content = (d.Content?.Length ?? 0) > 200 
                        ? d.Content!.Substring(0, 200) + "..." 
                        : d.Content ?? "",
                    RelevanceScore = 1.0 // All documents are used
                }).ToList();

                result.Success = true;
                result.Metadata["documents_used"] = documents.Count;
                result.Metadata["total_chunks"] = documents.Sum(d => d.Chunks.Count);

                _logger.LogInformation("[FileFirstStrategy] Generated answer using {DocCount} documents, {ChunkCount} chunks", 
                    documents.Count, documents.Sum(d => d.Chunks.Count));
            }
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
}
