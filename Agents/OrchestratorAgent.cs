using RagAgentApi.Models;
using RagAgentApi.Services;

namespace RagAgentApi.Agents;

/// <summary>
/// Orchestrates the execution of all other agents in the RAG pipeline
/// </summary>
public class OrchestratorAgent : BaseRagAgent
{
    private readonly ScraperAgent _scraperAgent;
 private readonly ChunkerAgent _chunkerAgent;
    private readonly EmbeddingAgent _embeddingAgent;
 private readonly StorageAgent _storageAgent;
    private readonly AgentOrchestrationService _orchestrationService;

    public OrchestratorAgent(
       ScraperAgent scraperAgent,
      ChunkerAgent chunkerAgent,
    EmbeddingAgent embeddingAgent,
     StorageAgent storageAgent,
   AgentOrchestrationService orchestrationService,
 ILogger<OrchestratorAgent> logger) : base(logger)
    {
        _scraperAgent = scraperAgent;
        _chunkerAgent = chunkerAgent;
    _embeddingAgent = embeddingAgent;
        _storageAgent = storageAgent;
_orchestrationService = orchestrationService;
  }

    public override string Name => "OrchestratorAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
     var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

        try
        {
            _logger.LogInformation("[Orchestrator] Starting pipeline for thread {ThreadId}", context.ThreadId);

            // Step 1: Scrape content
  AddMessage(context, _scraperAgent.Name, "Starting content scraping", new Dictionary<string, object>
 {
                { "url", context.State.GetValueOrDefault("url", "") }
   });

     var scrapingResult = await _scraperAgent.ExecuteAsync(context, cancellationToken);
  if (!scrapingResult.Success)
       {
           _logger.LogError("[Orchestrator] Scraping failed for thread {ThreadId}: {Error}", 
         context.ThreadId, scrapingResult.Message);
  return scrapingResult;
       }

       // Step 2: Chunk content
     AddMessage(context, _chunkerAgent.Name, "Starting content chunking");
 
     var chunkingResult = await _chunkerAgent.ExecuteAsync(context, cancellationToken);
       if (!chunkingResult.Success)
      {
  _logger.LogError("[Orchestrator] Chunking failed for thread {ThreadId}: {Error}", 
   context.ThreadId, chunkingResult.Message);
    return chunkingResult;
       }

// Step 3: Generate embeddings
   AddMessage(context, _embeddingAgent.Name, "Starting embedding generation");

    var embeddingResult = await _embeddingAgent.ExecuteAsync(context, cancellationToken);
    if (!embeddingResult.Success)
   {
    _logger.LogError("[Orchestrator] Embedding failed for thread {ThreadId}: {Error}", 
        context.ThreadId, embeddingResult.Message);
      return embeddingResult;
  }

            // Step 4: Store in search index
    AddMessage(context, _storageAgent.Name, "Starting document storage");

      var storageResult = await _storageAgent.ExecuteAsync(context, cancellationToken);
   if (!storageResult.Success)
 {
  _logger.LogError("[Orchestrator] Storage failed for thread {ThreadId}: {Error}", 
      context.ThreadId, storageResult.Message);
      return storageResult;
        }

   // Update orchestration service
      _orchestrationService.UpdateContext(context);

            var chunksProcessed = context.State.ContainsKey("chunks") ? 
  ((List<string>)context.State["chunks"]).Count : 0;

   AddMessage(context, "System", "Pipeline completed successfully", new Dictionary<string, object>
      {
  { "chunks_processed", chunksProcessed }
       });

    stopwatch.Stop();
         LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

      _logger.LogInformation("[Orchestrator] Pipeline completed successfully for thread {ThreadId}. Processed {ChunkCount} chunks", 
    context.ThreadId, chunksProcessed);

  return AgentResult.CreateSuccess(
"RAG pipeline completed successfully",
     new Dictionary<string, object>
   {
  { "thread_id", context.ThreadId },
              { "chunks_processed", chunksProcessed },
     { "execution_time_ms", stopwatch.ElapsedMilliseconds }
      });
        }
 catch (Exception ex)
{
            stopwatch.Stop();
     LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
    return HandleException(ex, context.ThreadId, "pipeline execution");
      }
    }
}