using RagAgentApi.Models;
using RagAgentApi.Services;

namespace RagAgentApi.Agents;

/// <summary>
/// Generates embeddings for text chunks using Azure OpenAI
/// </summary>
public class EmbeddingAgent : BaseRagAgent
{
 private readonly IAzureOpenAIService _openAIService;
    private readonly IConfiguration _configuration;

   public EmbeddingAgent(IAzureOpenAIService openAIService, IConfiguration configuration, ILogger<EmbeddingAgent> logger) : base(logger)
 {
   _openAIService = openAIService;
        _configuration = configuration;
    }

    public override string Name => "EmbeddingAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
  var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

      try
   {
            if (!context.State.TryGetValue("chunks", out var chunksObj) || chunksObj is not List<string> chunks)
{
 return AgentResult.CreateFailure("Chunks not found in context state");
   }

  if (!chunks.Any())
        {
      return AgentResult.CreateFailure("No chunks to process for embeddings");
     }

   _logger.LogInformation("[EmbeddingAgent] Generating embeddings for {Count} chunks", chunks.Count);

 // Generate embeddings in batches
            var embeddings = await _openAIService.GetEmbeddingsAsync(chunks, cancellationToken);

 if (embeddings.Count != chunks.Count)
      {
        return AgentResult.CreateFailure(
   $"Embedding count mismatch: expected {chunks.Count}, got {embeddings.Count}");
    }

      // Validate embedding dimensions
      var expectedDimensions = _configuration.GetValue<int>("RagSettings:VectorDimensions", 1536);
     var invalidEmbeddings = embeddings.Where(e => e.Length != expectedDimensions).ToList();

        if (invalidEmbeddings.Any())
     {
       return AgentResult.CreateFailure(
          $"Invalid embedding dimensions: expected {expectedDimensions}, but {invalidEmbeddings.Count} embeddings have different dimensions");
   }

 // Store embeddings in context state
  context.State["embeddings"] = embeddings;

     stopwatch.Stop();
   LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

   _logger.LogInformation("[EmbeddingAgent] Created {Count} embeddings", embeddings.Count);

 AddMessage(context, "StorageAgent", $"Generated {embeddings.Count} embeddings",
   new Dictionary<string, object>
  {
{ "embedding_count", embeddings.Count },
           { "dimensions", expectedDimensions }
         });

  return AgentResult.CreateSuccess(
        "Embeddings generated successfully",
       new Dictionary<string, object>
  {
    { "embedding_count", embeddings.Count },
        { "dimensions", expectedDimensions },
       { "processing_time_ms", stopwatch.ElapsedMilliseconds }
          });
}
        catch (Exception ex)
        {
    stopwatch.Stop();
 LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
     return HandleException(ex, context.ThreadId, "embedding generation");
        }
    }
}