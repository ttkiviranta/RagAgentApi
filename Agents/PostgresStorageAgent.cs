using RagAgentApi.Models;
using RagAgentApi.Models.PostgreSQL;
using RagAgentApi.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RagAgentApi.Agents;

/// <summary>
/// PostgreSQL-based storage agent for documents and embeddings
/// </summary>
public class PostgresStorageAgent : BaseRagAgent
{
    private readonly RagDbContext _context;

    public PostgresStorageAgent(RagDbContext context, ILogger<PostgresStorageAgent> logger) : base(logger)
  {
        _context = context;
    }

    public override string Name => "PostgresStorageAgent";

  public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

        try
        {
         // Validate required data in context
  if (!context.State.TryGetValue("url", out var urlObj) || urlObj is not string url)
      {
       return AgentResult.CreateFailure("URL not found in context state");
            }

            if (!context.State.TryGetValue("chunks", out var chunksObj) || chunksObj is not List<string> chunks)
            {
  return AgentResult.CreateFailure("Chunks not found in context state");
   }

            if (!context.State.TryGetValue("embeddings", out var embeddingsObj) || embeddingsObj is not List<float[]> embeddings)
            {
return AgentResult.CreateFailure("Embeddings not found in context state");
            }

     if (!context.State.TryGetValue("raw_content", out var rawContentObj) || rawContentObj is not string rawContent)
            {
 return AgentResult.CreateFailure("Raw content not found in context state");
  }

    if (chunks.Count != embeddings.Count)
        {
                return AgentResult.CreateFailure(
          $"Chunk and embedding count mismatch: {chunks.Count} chunks, {embeddings.Count} embeddings");
            }

        _logger.LogInformation("[PostgresStorageAgent] Storing {Count} chunks for URL: {Url}", chunks.Count, url);

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
     {
    // Create or update document
   var document = await CreateOrUpdateDocumentAsync(url, rawContent, context, cancellationToken);

      // Create document chunks with embeddings
     var documentChunks = await CreateDocumentChunksAsync(document.Id, chunks, embeddings, cancellationToken);

      await _context.SaveChangesAsync(cancellationToken);
  await transaction.CommitAsync(cancellationToken);

  // Update context state
   context.State["document_id"] = document.Id;
     context.State["documents_stored"] = 1;
       context.State["chunks_stored"] = documentChunks.Count;
                context.State["storage_completed_at"] = DateTimeOffset.UtcNow;

       stopwatch.Stop();
     LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

  _logger.LogInformation("[PostgresStorageAgent] Stored document {DocumentId} with {ChunkCount} chunks", 
         document.Id, documentChunks.Count);

        AddMessage(context, "System", $"Stored document with {documentChunks.Count} chunks in PostgreSQL",
         new Dictionary<string, object>
        {
   { "document_id", document.Id },
  { "chunks_stored", documentChunks.Count },
             { "url", url }
         });

         return AgentResult.CreateSuccess(
        "Documents stored successfully in PostgreSQL",
   new Dictionary<string, object>
        {
                 { "document_id", document.Id },
            { "chunks_stored", documentChunks.Count },
              { "url", url },
          { "storage_time_ms", stopwatch.ElapsedMilliseconds }
         });
            }
     catch (Exception)
            {
      await transaction.RollbackAsync(cancellationToken);
           throw;
}
        }
     catch (Exception ex)
      {
  stopwatch.Stop();
      LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
     return HandleException(ex, context.ThreadId, "PostgreSQL document storage");
        }
    }

    private async Task<Document> CreateOrUpdateDocumentAsync(string url, string content, AgentContext context, CancellationToken cancellationToken)
    {
  var urlHash = ComputeMD5Hash(url);
      var contentHash = ComputeMD5Hash(content);

  // Check if document already exists
        var existingDocument = await _context.Documents
            .FirstOrDefaultAsync(d => d.UrlHash == urlHash, cancellationToken);

        if (existingDocument != null)
   {
        // Check if content changed
            if (existingDocument.ContentHash != contentHash)
   {
              _logger.LogInformation("[PostgresStorageAgent] Content changed for URL: {Url}, updating document", url);

     // Remove existing chunks
      var existingChunks = await _context.DocumentChunks
               .Where(dc => dc.DocumentId == existingDocument.Id)
         .ToListAsync(cancellationToken);

                _context.DocumentChunks.RemoveRange(existingChunks);

        // Update document
      existingDocument.Content = content;
            existingDocument.ContentHash = contentHash;
existingDocument.LastUpdated = DateTime.UtcNow;
                existingDocument.ScrapingDurationMs = context.State.TryGetValue("execution_time_ms", out var durationObj) && durationObj is int duration ? duration : null;

         // Update metadata
    var metadata = new
         {
          thread_id = context.ThreadId.ToString(),
       chunk_size = context.State.GetValueOrDefault("chunk_size", 1000),
     chunk_overlap = context.State.GetValueOrDefault("chunk_overlap", 200),
  updated_at = DateTimeOffset.UtcNow
     };
 existingDocument.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(metadata));
      }
 else
     {
       _logger.LogInformation("[PostgresStorageAgent] No content change for URL: {Url}, skipping update", url);
          }

   return existingDocument;
    }
        else
        {
            // Create new document
       _logger.LogInformation("[PostgresStorageAgent] Creating new document for URL: {Url}", url);

            var metadata = new
            {
     thread_id = context.ThreadId.ToString(),
        chunk_size = context.State.GetValueOrDefault("chunk_size", 1000),
   chunk_overlap = context.State.GetValueOrDefault("chunk_overlap", 200),
    created_at = DateTimeOffset.UtcNow
            };

            var newDocument = new Document
     {
Url = url,
      UrlHash = urlHash,
     Content = content,
ContentHash = contentHash,
      ScrapedAt = DateTime.UtcNow,
       LastUpdated = DateTime.UtcNow,
       Status = "active",
                ScrapingDurationMs = context.State.TryGetValue("execution_time_ms", out var durationObj) && durationObj is int duration ? duration : null,
                Metadata = JsonDocument.Parse(JsonSerializer.Serialize(metadata))
            };

 _context.Documents.Add(newDocument);
            return newDocument;
 }
    }

    private async Task<List<DocumentChunk>> CreateDocumentChunksAsync(Guid documentId, List<string> chunks, List<float[]> embeddings, CancellationToken cancellationToken)
    {
        var documentChunks = new List<DocumentChunk>();

   for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = new DocumentChunk
 {
           DocumentId = documentId,
    ChunkIndex = i,
             Content = chunks[i],
     TokenCount = EstimateTokenCount(chunks[i]),
       Embedding = new Vector(embeddings[i]),
           CreatedAt = DateTime.UtcNow,
        ChunkMetadata = JsonDocument.Parse(JsonSerializer.Serialize(new
             {
       chunk_index = i,
        content_length = chunks[i].Length,
            estimated_tokens = EstimateTokenCount(chunks[i])
       }))
  };

       documentChunks.Add(chunk);
    _context.DocumentChunks.Add(chunk);
        }

        _logger.LogDebug("[PostgresStorageAgent] Created {Count} document chunks", documentChunks.Count);
  return documentChunks;
    }

    private static string ComputeMD5Hash(string input)
    {
    using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
     return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int EstimateTokenCount(string text)
    {
        // Simple token estimation: ~4 characters per token
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}