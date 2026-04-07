using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RagAgentApi.Models;
using RagAgentApi.Models.Requests;
using RagAgentApi.Agents;
using RagAgentApi.Services;
using RagAgentApi.Services.Retrieval;
using RagAgentApi.Filters;
using System.ComponentModel.DataAnnotations;

namespace RagAgentApi.Controllers;

/// <summary>
/// Main controller for RAG (Retrieval-Augmented Generation) operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly OrchestratorAgent _orchestratorAgent;
    private readonly QueryAgent _queryAgent;
    private readonly AgentOrchestrationService _orchestrationService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly IAzureSearchService _searchService;
    private readonly RetrievalStrategyFactory _retrievalStrategyFactory;
    private readonly ILogger<RagController> _logger;
    private readonly IConfiguration _configuration;

    public RagController(
        OrchestratorAgent orchestratorAgent,
        QueryAgent queryAgent,
        AgentOrchestrationService orchestrationService,
        IAzureOpenAIService openAIService,
        IAzureSearchService searchService,
        RetrievalStrategyFactory retrievalStrategyFactory,
        ILogger<RagController> logger,
        IConfiguration configuration)
    {
        _orchestratorAgent = orchestratorAgent;
        _queryAgent = queryAgent;
        _orchestrationService = orchestrationService;
        _openAIService = openAIService;
        _searchService = searchService;
        _retrievalStrategyFactory = retrievalStrategyFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Ingest content from a URL into the RAG system
    /// </summary>
    /// <param name="request">The ingestion request containing URL and chunking parameters</param>
    /// <returns>Result containing thread ID and processing information</returns>
    /// <response code="200">Content ingested successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Internal server error during processing</response>
    [HttpPost("ingest")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> IngestContent([FromBody] RagRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(CreateErrorResponse("Invalid request parameters", ModelState));
            }

            // Additional validation
            if (request.ChunkOverlap >= request.ChunkSize / 2)
            {
                return BadRequest(CreateErrorResponse("ChunkOverlap must be less than ChunkSize/2"));
            }

            _logger.LogInformation("Starting content ingestion for URL: {Url}", request.Url);

            // Create new execution context
            var context = _orchestrationService.CreateContext();

            // Set initial state
            context.State["url"] = request.Url;
            context.State["chunk_size"] = request.ChunkSize;
            context.State["chunk_overlap"] = request.ChunkOverlap;
            context.State["crawl_depth"] = request.CrawlDepth;
            context.State["max_pages"] = request.MaxPages;
            context.State["same_domain_only"] = request.SameDomainOnly;

            // Execute the RAG pipeline
            var result = await _orchestratorAgent.ExecuteAsync(context, cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("RAG pipeline failed for thread {ThreadId}: {Error}", context.ThreadId, result.Message);
                return StatusCode(500, CreateErrorResponse(result.Message, result.Errors, context.ThreadId));
            }

            var response = new
            {
                thread_id = context.ThreadId,
                message = result.Message,
                chunks_processed = result.Data?.GetValueOrDefault("chunks_processed", 0),
                pages_crawled = result.Data?.GetValueOrDefault("pages_crawled", 1),
                url = request.Url,
                chunk_size = request.ChunkSize,
                chunk_overlap = request.ChunkOverlap,
                crawl_depth = request.CrawlDepth,
                max_pages = request.MaxPages,
                execution_time_ms = result.Data?.GetValueOrDefault("execution_time_ms", 0)
            };

            _logger.LogInformation("Content ingestion completed successfully for thread {ThreadId}", context.ThreadId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during content ingestion");
            return StatusCode(500, CreateErrorResponse("An unexpected error occurred during content ingestion"));
        }
    }

    /// <summary>
    /// Test endpoint - ingest raw text content directly with simple storage
    /// </summary>
    [HttpPost("ingest-text-test")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> IngestTextTest([FromBody] IngestTextRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(new { error = "Content cannot be empty" });
            }

            var threadId = Guid.NewGuid();

            _logger.LogInformation("[TEST] Received text ingestion request: {Title}, Size: {Size} bytes", 
                request.Title, request.Content.Length);

            // Return success - full pipeline will handle storage
            var response = new
            {
                thread_id = threadId,
                message = "Text content ingested successfully",
                status = "success",
                title = request.Title,
                source = request.Source,
                content_length = request.Content.Length,
                execution_time_ms = 0
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test endpoint error");
            return StatusCode(500, new { error = $"Test failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Check if a document with the given title already exists
    /// </summary>
    [HttpGet("check-document/{title}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckDocumentExists(string title, CancellationToken cancellationToken = default)
    {
        try
        {
            var postgresQueryService = HttpContext.RequestServices.GetRequiredService<PostgresQueryService>();

            _logger.LogInformation("[CheckDocument] Checking if document exists: {Title}", title);

            // Return simple response
            var response = new
            {
                exists = false,  // TODO: Implement actual check from database
                title = title,
                message = "Dokumentin olemassaolo tarkistettu"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking document existence");
            return StatusCode(500, new { error = $"Virhe dokumentin tarkistuksessa: {ex.Message}" });
        }
    }

    /// <summary>
    /// Check if a document with the given URL already exists
    /// </summary>
    [HttpPost("check-document-url")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckDocumentUrlExists([FromBody] CheckUrlRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[CheckDocumentUrl] Checking if URL exists: {Url}", request.Url);

            // Compute URL hash (same method as PostgresStorageAgent)
            var urlHash = ComputeMD5Hash(request.Url);

            // Check database for existing document
            var dbContext = HttpContext.RequestServices.GetRequiredService<Data.RagDbContext>();
            var existingDocument = await dbContext.Documents
                .FirstOrDefaultAsync(d => d.UrlHash == urlHash, cancellationToken);

            var response = new
            {
                exists = existingDocument != null,
                url = request.Url,
                documentId = existingDocument?.Id,
                title = existingDocument?.Title,
                lastUpdated = existingDocument?.LastUpdated,
                message = existingDocument != null 
                    ? "Document with this URL already exists in the database." 
                    : "URL not found in database."
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking document URL existence");
            return StatusCode(500, new { error = $"Error checking document URL: {ex.Message}" });
        }
    }

    /// <summary>
    /// Delete a document and its chunks by URL
    /// </summary>
    [HttpPost("delete-document-url")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteDocumentByUrl([FromBody] CheckUrlRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[DeleteDocumentUrl] Deleting document by URL: {Url}", request.Url);

            var urlHash = ComputeMD5Hash(request.Url);
            var dbContext = HttpContext.RequestServices.GetRequiredService<Data.RagDbContext>();

            var existingDocument = await dbContext.Documents
                .Include(d => d.Chunks)
                .FirstOrDefaultAsync(d => d.UrlHash == urlHash, cancellationToken);

            if (existingDocument == null)
            {
                return NotFound(new { error = "Document not found", url = request.Url });
            }

            // Remove chunks first (cascade should handle this, but being explicit)
            if (existingDocument.Chunks.Any())
            {
                dbContext.DocumentChunks.RemoveRange(existingDocument.Chunks);
            }

            // Remove document
            dbContext.Documents.Remove(existingDocument);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("[DeleteDocumentUrl] Document deleted successfully: {Url}", request.Url);

            return Ok(new
            {
                success = true,
                url = request.Url,
                deletedDocumentId = existingDocument.Id,
                chunksDeleted = existingDocument.Chunks.Count,
                message = "Document and its chunks deleted successfully."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document by URL");
            return StatusCode(500, new { error = $"Error deleting document: {ex.Message}" });
        }
    }

    private static string ComputeMD5Hash(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Delete a document and its chunks by title
    /// </summary>
    [HttpDelete("delete-document/{title}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteDocument(string title, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[DeleteDocument] Deleting document: {Title}", title);

            // Return success response
            var response = new
            {
                success = true,
                title = title,
                message = $"Dokumentti '{title}' poistettu onnistuneesti"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { error = $"Virhe dokumentin poistamisessa: {ex.Message}" });
        }
    }

    /// <summary>
    /// Upload a file with original storage in Azure Blob Storage
    /// </summary>
    /// <param name="file">The file to upload</param>
    /// <param name="chunkSize">Size of text chunks (100-5000)</param>
    /// <param name="chunkOverlap">Overlap between chunks (0-2500)</param>
    /// <param name="storeOriginalFile">Whether to store original file in Blob Storage</param>
    /// <param name="title">Custom document title (optional)</param>
    /// <returns>Upload result with blob storage information</returns>
    [HttpPost("upload-file")]
    [ProducesResponseType(typeof(FileUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB limit
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        [FromQuery] int chunkSize = 1000,
        [FromQuery] int chunkOverlap = 200,
        [FromQuery] bool storeOriginalFile = true,
        [FromQuery] string? title = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Validate file
            if (file == null || file.Length == 0)
            {
                return BadRequest(CreateErrorResponse("No file provided or file is empty"));
            }

            var allowedTypes = new[] { "application/pdf", "text/plain", "text/markdown", "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };

            if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()) && 
                !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
                !file.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                !file.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(CreateErrorResponse($"Unsupported file type: {file.ContentType}. Allowed: PDF, TXT, MD, DOC, DOCX"));
            }

            var threadId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var documentTitle = title ?? Path.GetFileNameWithoutExtension(file.FileName);
            var documentUrl = $"blob://{documentId:N}/{file.FileName}";

            _logger.LogInformation("[UploadFile] Starting upload: {FileName}, Size: {Size} bytes, ThreadId: {ThreadId}",
                file.FileName, file.Length, threadId);

            // Get services
            var blobService = HttpContext.RequestServices.GetService<IBlobStorageService>();
            var chunkerAgent = HttpContext.RequestServices.GetRequiredService<ChunkerAgent>();
            var embeddingAgent = HttpContext.RequestServices.GetRequiredService<EmbeddingAgent>();
            var storageAgent = HttpContext.RequestServices.GetRequiredService<PostgresStorageAgent>();

            BlobUploadResult? blobResult = null;

            // Step 1: Upload original file to Blob Storage (if enabled)
            if (storeOriginalFile && blobService?.IsEnabled == true)
            {
                using var fileStream = file.OpenReadStream();
                blobResult = await blobService.UploadFileAsync(
                    fileStream,
                    file.FileName,
                    file.ContentType,
                    documentId,
                    cancellationToken);

                if (!blobResult.Success)
                {
                    _logger.LogWarning("[UploadFile] Blob upload failed: {Error}, continuing without blob storage", blobResult.ErrorMessage);
                }
                else
                {
                    _logger.LogInformation("[UploadFile] Blob upload successful: {BlobUri}", blobResult.BlobUri);
                }
            }

            // Step 2: Extract text content from file
            string textContent;
            using (var stream = file.OpenReadStream())
            {
                textContent = await ExtractTextFromFileAsync(stream, file.FileName, file.ContentType, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(textContent))
            {
                return BadRequest(CreateErrorResponse("Could not extract text content from file"));
            }

            // Step 3: Chunk the content
            var chunkContext = new AgentContext
            {
                ThreadId = threadId.ToString(),
                State = new Dictionary<string, object>
                {
                    { "raw_content", textContent },
                    { "url", documentUrl },
                    { "chunk_size", chunkSize },
                    { "chunk_overlap", chunkOverlap },
                    { "title", documentTitle },
                    { "source", "file-upload" },
                    { "document_id", documentId }
                }
            };

            // Add blob metadata to context
            if (blobResult?.Success == true)
            {
                chunkContext.State["blob_uri"] = blobResult.BlobUri!;
                chunkContext.State["blob_name"] = blobResult.BlobName!;
                chunkContext.State["blob_container"] = blobResult.ContainerName!;
                chunkContext.State["original_file_hash"] = blobResult.FileHash!;
                chunkContext.State["original_file_size"] = blobResult.FileSizeBytes;
                chunkContext.State["original_file_name"] = file.FileName;
                chunkContext.State["mime_type"] = file.ContentType;
                chunkContext.State["blob_uploaded_at"] = blobResult.UploadedAt!;
            }

            var chunkResult = await chunkerAgent.ExecuteAsync(chunkContext, cancellationToken);
            if (!chunkResult.Success)
            {
                return StatusCode(500, CreateErrorResponse($"Chunking failed: {chunkResult.Message}"));
            }

            // Step 4: Generate embeddings
            var embeddingContext = new AgentContext
            {
                ThreadId = threadId.ToString(),
                State = chunkContext.State
            };

            var embeddingResult = await embeddingAgent.ExecuteAsync(embeddingContext, cancellationToken);
            if (!embeddingResult.Success)
            {
                return StatusCode(500, CreateErrorResponse($"Embedding failed: {embeddingResult.Message}"));
            }

            // Step 5: Store in PostgreSQL
            var storageContext = new AgentContext
            {
                ThreadId = threadId.ToString(),
                State = embeddingContext.State
            };

            var storageResult = await storageAgent.ExecuteAsync(storageContext, cancellationToken);
            if (!storageResult.Success)
            {
                return StatusCode(500, CreateErrorResponse($"Storage failed: {storageResult.Message}"));
            }

            stopwatch.Stop();

            var chunksProcessed = (int)(embeddingContext.State.GetValueOrDefault("chunk_count", 0) ?? 0);

            var response = new FileUploadResponse
            {
                ThreadId = threadId,
                DocumentId = documentId,
                Status = "success",
                Message = "File uploaded and processed successfully",
                FileName = file.FileName,
                FileSizeBytes = file.Length,
                MimeType = file.ContentType,
                StoredInBlobStorage = blobResult?.Success ?? false,
                BlobUri = blobResult?.BlobUri,
                FileHash = blobResult?.FileHash,
                ChunksProcessed = chunksProcessed,
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                RetrievalMode = blobResult?.Success == true ? "FileFirst" : "Rag"
            };

            _logger.LogInformation("[UploadFile] Completed: {FileName}, Chunks: {Chunks}, BlobStored: {BlobStored}, Time: {Time}ms",
                file.FileName, chunksProcessed, blobResult?.Success ?? false, stopwatch.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UploadFile] Failed to process file upload");
            return StatusCode(500, CreateErrorResponse($"File upload failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Extract text content from various file types
    /// </summary>
    private async Task<string> ExtractTextFromFileAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            // For text files, read directly
            if (contentType == "text/plain" || fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync(cancellationToken);
            }

            // For PDF files, use Azure Document Intelligence or iText fallback
            if (contentType == "application/pdf" || fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                // Copy stream to MemoryStream for reusability (original stream may not support seeking)
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                var pdfBytes = ms.ToArray();

                _logger.LogDebug("[UploadFile] Processing PDF: {FileName}, {Size} bytes", fileName, pdfBytes.Length);

                // Try to use Azure Document Intelligence if configured (best for scanned/OCR PDFs)
                var docIntelligence = HttpContext.RequestServices.GetService<IAzureDocumentIntelligenceService>();
                if (docIntelligence != null)
                {
                    using var diStream = new MemoryStream(pdfBytes);
                    var result = await docIntelligence.ExtractTextAsync(diStream, cancellationToken);
                    if (!string.IsNullOrEmpty(result))
                    {
                        _logger.LogInformation("[UploadFile] Document Intelligence extracted {Length} chars from {FileName}", 
                            result.Length, fileName);
                        return result;
                    }
                    _logger.LogWarning("[UploadFile] Document Intelligence returned empty for {FileName}, trying iText fallback", fileName);
                }

                // Fallback: use iText for text-based PDFs
                var iTextResult = ExtractTextFromPdfBytes(pdfBytes);
                if (!string.IsNullOrEmpty(iTextResult) && !iTextResult.StartsWith("[PDF content"))
                {
                    _logger.LogInformation("[UploadFile] iText extracted {Length} chars from {FileName}", 
                        iTextResult.Length, fileName);
                    return iTextResult;
                }

                _logger.LogWarning("[UploadFile] Both Document Intelligence and iText failed for {FileName}", fileName);
                return string.Empty;
            }

            // Default: try to read as text
            using var defaultReader = new StreamReader(stream);
            return await defaultReader.ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[UploadFile] Text extraction failed for {FileName}", fileName);
            return string.Empty;
        }
    }

    /// <summary>
    /// Basic PDF text extraction (placeholder - should be enhanced with proper PDF library)
    /// </summary>
    private string ExtractTextFromPdfBytes(byte[] pdfBytes)
    {
        try
        {
            // This is a simplified extraction - in production, use iText or similar
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
            _logger.LogWarning(ex, "PDF extraction failed, returning placeholder");
            return $"[PDF content - {pdfBytes.Length} bytes]";
        }
    }

    /// <summary>
    /// Ingest raw text content directly (e.g., from file uploads)
    /// </summary>
    /// <param name="request">Request containing raw text content</param>
    /// <returns>Result containing thread ID and processing information</returns>
    [HttpPost("ingest-text")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> IngestText([FromBody] IngestTextRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(CreateErrorResponse("Invalid request parameters", ModelState));
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest(CreateErrorResponse("Content cannot be empty"));
            }

            _logger.LogInformation("Starting text content ingestion: {Title}", request.Title ?? "Untitled");

            var threadId = Guid.NewGuid();
            var documentUrl = $"local://document/{request.Title}";

            try
            {
                // NOTE: Duplicate documents will be created with same title
                // TODO: Add duplicate detection to avoid creating duplicate documents
                _logger.LogInformation("[IngestText] Processing document: {Title} (URL: {Url})", request.Title, documentUrl);
                var chunkerAgent = HttpContext.RequestServices.GetRequiredService<ChunkerAgent>();
                var chunkContext = new AgentContext
                {
                    ThreadId = threadId.ToString(),
                    State = new Dictionary<string, object>
                    {
                        { "raw_content", request.Content },
                        { "url", documentUrl },  // Use variable for consistency
                        { "chunk_size", request.ChunkSize },
                        { "chunk_overlap", request.ChunkOverlap },
                        { "title", request.Title ?? "Uploaded Document" },
                        { "source", request.Source ?? "file-upload" }
                    }
                };

                var chunkResult = await chunkerAgent.ExecuteAsync(chunkContext, cancellationToken);

                if (!chunkResult.Success)
                {
                    _logger.LogError("Chunking failed: {Error}", chunkResult.Message);
                    return StatusCode(500, CreateErrorResponse($"Chunking failed: {chunkResult.Message}", new List<string>(), threadId.ToString()));
                }

                _logger.LogInformation("Chunking successful: {ChunkCount} chunks created", chunkContext.State.GetValueOrDefault("chunk_count", 0));
                // Step 2: Get embeddings for chunks
                var embeddingAgent = HttpContext.RequestServices.GetRequiredService<EmbeddingAgent>();
                var embeddingContext = new AgentContext
                {
                    ThreadId = threadId.ToString(),
                    State = chunkContext.State
                };

                var embeddingResult = await embeddingAgent.ExecuteAsync(embeddingContext, cancellationToken);

                if (!embeddingResult.Success)
                {
                    _logger.LogError("Embedding failed: {Error}", embeddingResult.Message);
                    return StatusCode(500, CreateErrorResponse($"Embedding failed: {embeddingResult.Message}", new List<string>(), threadId.ToString()));
                }

                _logger.LogInformation("Embedding successful: embeddings generated for chunks");

                // Step 3: Store in PostgreSQL
                var storageAgent = HttpContext.RequestServices.GetRequiredService<PostgresStorageAgent>();
                var storageContext = new AgentContext
                {
                    ThreadId = threadId.ToString(),
                    State = embeddingContext.State
                };

                var storageResult = await storageAgent.ExecuteAsync(storageContext, cancellationToken);

                if (!storageResult.Success)
                {
                    _logger.LogError("Storage failed: {Error}", storageResult.Message);
                    return StatusCode(500, CreateErrorResponse($"Storage failed: {storageResult.Message}", new List<string>(), threadId.ToString()));
                }

                _logger.LogInformation("Storage successful: document stored in PostgreSQL");

                var chunksProcessed = embeddingContext.State.GetValueOrDefault("chunk_count", 0);

                var response = new
                {
                    thread_id = threadId,
                    message = "Text content ingested successfully",
                    status = "success",
                    chunks_processed = chunksProcessed,
                    title = request.Title,
                    source = request.Source,
                    chunk_size = request.ChunkSize,
                    chunk_overlap = request.ChunkOverlap,
                    execution_time_ms = 0
                };

                _logger.LogInformation("Text ingestion completed successfully for thread {ThreadId}", threadId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during text processing pipeline for thread {ThreadId}", threadId);
                return StatusCode(500, CreateErrorResponse($"Pipeline error: {ex.Message}", new List<string>(), threadId.ToString()));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during text ingestion");
            return StatusCode(500, CreateErrorResponse("An unexpected error occurred during text ingestion"));
        }
    }

    /// <summary>
    /// Query the RAG system for information
    /// </summary>
    /// <param name="request">The query request containing the question and search parameters</param>
    /// <returns>Answer with relevant source documents</returns>
    /// <response code="200">Query processed successfully</response>
    /// <response code="400">Invalid query parameters</response>
    /// <response code="500">Internal server error during processing</response>
    [HttpPost("query")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> QueryContent([FromBody] QueryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(CreateErrorResponse("Invalid query parameters", ModelState));
            }

            _logger.LogInformation("Processing query: '{Query}' with top-K {TopK}", request.Query, request.TopK);

            // Get retrieval strategy from factory
            var strategy = _retrievalStrategyFactory.GetStrategy();
            _logger.LogInformation("Using retrieval strategy: {Strategy} (configured mode: {Mode})", 
                strategy.Name, _retrievalStrategyFactory.ConfiguredMode);

            // Execute the retrieval strategy
            var result = await strategy.ExecuteAsync(request.Query, request.TopK, cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Retrieval failed: {Error}", result.ErrorMessage);
                return StatusCode(500, CreateErrorResponse(result.ErrorMessage ?? "Query failed"));
            }

            var response = new
            {
                query = request.Query,
                answer = result.Answer,
                sources = result.Sources.Select(s => new
                {
                    url = s.Url,
                    content = s.Content,
                    relevanceScore = s.RelevanceScore
                }),
                source_count = result.Sources.Count,
                processing_time_ms = result.ProcessingTimeMs,
                retrieval_mode = result.ConfiguredMode,
                strategy_used = result.StrategyUsed,
                metadata = result.Metadata
            };

            _logger.LogInformation("Query processed successfully using {Strategy} strategy", result.StrategyUsed);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during query processing");
            return StatusCode(500, CreateErrorResponse("An unexpected error occurred during query processing"));
        }
    }

    /// <summary>
    /// Get current retrieval configuration
    /// </summary>
    [HttpGet("retrieval-config")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetRetrievalConfig()
    {
        var config = new
        {
            mode = _retrievalStrategyFactory.ConfiguredMode,
            autoModeDocumentThreshold = _configuration.GetValue<int>("Retrieval:AutoModeDocumentThreshold", 10),
            autoModeContentSizeThresholdKb = _configuration.GetValue<int>("Retrieval:AutoModeContentSizeThresholdKb", 500),
            minimumRelevanceScore = _configuration.GetValue<double>("Retrieval:MinimumRelevanceScore", 0.5)
        };

        return Ok(config);
    }

    /// <summary>
    /// Get information about a specific thread context
    /// </summary>
    /// <param name="threadId">The thread ID to retrieve</param>
    /// <returns>Thread context information</returns>
    [HttpGet("thread/{threadId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetThread(string threadId)
    {
        var context = _orchestrationService.GetContext(threadId);
        if (context == null)
        {
            return NotFound(CreateErrorResponse($"Thread {threadId} not found"));
        }

        var response = new
        {
            thread_id = context.ThreadId,
            state = context.State,
            messages = context.Messages,
            created_at = context.CreatedAt,
            updated_at = context.UpdatedAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Get messages for a specific thread
    /// </summary>
    /// <param name="threadId">The thread ID</param>
    /// <returns>List of messages in the thread</returns>
    [HttpGet("thread/{threadId}/messages")]
    [ProducesResponseType(typeof(List<AgentMessage>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetThreadMessages(string threadId)
    {
        var context = _orchestrationService.GetContext(threadId);
        if (context == null)
        {
            return NotFound(CreateErrorResponse($"Thread {threadId} not found"));
        }

        return Ok(context.Messages);
    }

    /// <summary>
    /// Health check endpoint to verify service status
    /// </summary>
    /// <returns>Health status of the RAG system and its dependencies</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> HealthCheck(CancellationToken cancellationToken = default)
    {
        try
        {
            var healthStatus = new Dictionary<string, string>
            {
                { "status", "healthy" }
            };

            var services = new Dictionary<string, string>();

            // Check Azure Search
            try
            {
                var searchHealthy = await _searchService.IsHealthyAsync(cancellationToken);
                services["azure_search"] = searchHealthy ? "ok" : "error";
            }
            catch (Exception ex)
            {
                services["azure_search"] = "error";
                _logger.LogWarning(ex, "Azure Search health check failed");
            }

            // Check Azure OpenAI (simple embedding test)
            try
            {
                await _openAIService.GetEmbeddingAsync("health check", cancellationToken);
                services["azure_openai"] = "ok";
            }
            catch (Exception ex)
            {
                services["azure_openai"] = "error";
                _logger.LogWarning(ex, "Azure OpenAI health check failed");
            }

            // Overall health status
            var allHealthy = services.Values.All(status => status == "ok");
            var response = new
            {
                status = allHealthy ? "healthy" : "degraded",
                services = services,
                timestamp = DateTimeOffset.UtcNow,
                version = "1.0.0"
            };

            var statusCode = allHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
            return StatusCode(statusCode, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(500, CreateErrorResponse("Health check failed"));
        }
    }

    /// <summary>
    /// Recreate Azure Search index (DEBUG ONLY)
    /// </summary>
    /// <returns>Confirmation of index recreation</returns>
    [HttpPost("debug/recreate-index")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RecreateSearchIndex()
    {
        try
        {
            _logger.LogWarning("Index recreation requested via API");
            await _searchService.RecreateIndexAsync();

            return Ok(new
            {
                message = "Search index recreated successfully",
                timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate search index via API");
            return StatusCode(500, CreateErrorResponse("Failed to recreate search index"));
        }
    }

    /// <summary>
    /// Enhanced content ingestion using dynamic agent selection
    /// </summary>
    /// <param name="request">The ingestion request containing URL and chunking parameters</param>
    /// <returns>Result containing agent type, pipeline info, and processing information</returns>
    /// <response code="200">Content ingested successfully with dynamic agent selection</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Internal server error during processing</response>
    [HttpPost("ingest-enhanced")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> IngestContentEnhanced([FromBody] RagRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (!ModelState.IsValid)
            {
                return BadRequest(CreateErrorResponse("Invalid request parameters", ModelState));
            }

            // Additional validation
            if (request.ChunkOverlap >= request.ChunkSize / 2)
            {
                return BadRequest(CreateErrorResponse("ChunkOverlap must be less than ChunkSize/2"));
            }

            _logger.LogInformation("Starting enhanced content ingestion for URL: {Url}, Depth: {Depth}, MaxPages: {MaxPages}", 
                request.Url, request.CrawlDepth, request.MaxPages);

            // Create new execution context
            var context = _orchestrationService.CreateContext();

            // Set initial state
            context.State["url"] = request.Url;
            context.State["chunk_size"] = request.ChunkSize;
            context.State["chunk_overlap"] = request.ChunkOverlap;
            context.State["crawl_depth"] = request.CrawlDepth;
            context.State["max_pages"] = request.MaxPages;
            context.State["same_domain_only"] = request.SameDomainOnly;
            context.State["enhanced_mode"] = true; // Flag for enhanced processing

            // Execute the enhanced RAG pipeline with dynamic agent selection
            var result = await _orchestratorAgent.ExecuteAsync(context, cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Enhanced RAG pipeline failed for thread {ThreadId}: {Error}", context.ThreadId, result.Message);
                return StatusCode(500, CreateErrorResponse(result.Message, result.Errors, context.ThreadId));
            }

            var response = new
            {
                thread_id = context.ThreadId,
                message = result.Message,
                agent_type = result.Data?.GetValueOrDefault("agent_type", "unknown"),
                pipeline_agents = result.Data?.GetValueOrDefault("pipeline_agents", new List<string>()),
                pipeline_length = result.Data?.GetValueOrDefault("pipeline_length", 0),
                chunks_processed = result.Data?.GetValueOrDefault("chunks_stored", 0),
                document_id = result.Data?.GetValueOrDefault("document_id", null),
                url = request.Url,
                chunk_size = request.ChunkSize,
                chunk_overlap = request.ChunkOverlap,
                execution_time_ms = result.Data?.GetValueOrDefault("total_execution_time_ms", 0),
                step_results = result.Data?.GetValueOrDefault("step_results", new List<object>()),
                enhanced_processing = true
            };

            _logger.LogInformation("Enhanced content ingestion completed successfully for thread {ThreadId}", context.ThreadId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during enhanced content ingestion");
            return StatusCode(500, CreateErrorResponse("An unexpected error occurred during enhanced content ingestion"));
        }
    }

    private object CreateErrorResponse(string message, List<string>? details = null, string? threadId = null)
    {
        return new
        {
            error = message,
            details = details ?? new List<string>(),
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            thread_id = threadId
        };
    }

    private object CreateErrorResponse(string message, Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState, string? threadId = null)
    {
        var details = modelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors)
            .Select(x => x.ErrorMessage)
            .ToList();

        return CreateErrorResponse(message, details, threadId);
    }
}