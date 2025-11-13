using RagAgentApi.Models;
using RagAgentApi.Services;

namespace RagAgentApi.Agents;

/// <summary>
/// Stores document chunks and embeddings in Azure AI Search
/// </summary>
public class StorageAgent : BaseRagAgent
{
    private readonly IAzureSearchService _searchService;

  public StorageAgent(IAzureSearchService searchService, ILogger<StorageAgent> logger) : base(logger)
  {
        _searchService = searchService;
    }

    public override string Name => "StorageAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
 var stopwatch = System.Diagnostics.Stopwatch.StartNew();
     LogExecutionStart(context.ThreadId);

  try
{
        // Validate required data in context
   if (!context.State.TryGetValue("chunks", out var chunksObj) || chunksObj is not List<string> chunks)
            {
        return AgentResult.CreateFailure("Chunks not found in context state");
      }

       if (!context.State.TryGetValue("embeddings", out var embeddingsObj) || embeddingsObj is not List<float[]> embeddings)
    {
   return AgentResult.CreateFailure("Embeddings not found in context state");
        }

        if (!context.State.TryGetValue("source_url", out var sourceUrlObj) || sourceUrlObj is not string sourceUrl)
        {
return AgentResult.CreateFailure("Source URL not found in context state");
       }

  if (chunks.Count != embeddings.Count)
      {
   return AgentResult.CreateFailure(
      $"Chunk and embedding count mismatch: {chunks.Count} chunks, {embeddings.Count} embeddings");
      }

     _logger.LogInformation("[StorageAgent] Preparing to store {Count} documents", chunks.Count);

     // Ensure search index exists
     await _searchService.CreateOrUpdateIndexAsync(cancellationToken);

     // Create search documents
      var documents = new List<SearchDocument>();

  for (int i = 0; i < chunks.Count; i++)
      {
          var document = new SearchDocument
     {
       Id = $"{context.ThreadId}_{i}",
        Content = chunks[i],
              ContentVector = embeddings[i],
       SourceUrl = sourceUrl,
              ThreadId = context.ThreadId,
ChunkIndex = i,
   CreatedAt = DateTimeOffset.UtcNow
          };

          // Debug logging
     _logger.LogDebug("[StorageAgent] Creating document {Index}: Content length={Length}, URL='{URL}'",
   i, document.Content?.Length ?? 0, document.SourceUrl);
          
          documents.Add(document);
 }

        // Upload documents to search index
await _searchService.UploadDocumentsAsync(documents, cancellationToken);

  // Store additional metadata
    context.State["documents_stored"] = documents.Count;
  context.State["storage_completed_at"] = DateTimeOffset.UtcNow;

     stopwatch.Stop();
       LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

_logger.LogInformation("[StorageAgent] Stored {Count} documents to Azure Search", documents.Count);

      AddMessage(context, "System", $"Stored {documents.Count} documents successfully",
 new Dictionary<string, object>
      {
 { "documents_stored", documents.Count },
            { "source_url", sourceUrl },
        { "thread_id", context.ThreadId }
});

       return AgentResult.CreateSuccess(
     "Documents stored successfully",
  new Dictionary<string, object>
  {
             { "documents_stored", documents.Count },
   { "source_url", sourceUrl },
   { "thread_id", context.ThreadId },
        { "storage_time_ms", stopwatch.ElapsedMilliseconds }
       });
 }
      catch (Exception ex)
 {
     stopwatch.Stop();
       LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
 return HandleException(ex, context.ThreadId, "document storage");
  }
    }
}