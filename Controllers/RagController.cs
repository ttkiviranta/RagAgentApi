using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Models;
using RagAgentApi.Agents;
using RagAgentApi.Services;
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
    private readonly ILogger<RagController> _logger;

    public RagController(
        OrchestratorAgent orchestratorAgent,
        QueryAgent queryAgent,
 AgentOrchestrationService orchestrationService,
        IAzureOpenAIService openAIService,
     IAzureSearchService searchService,
 ILogger<RagController> logger)
    {
        _orchestratorAgent = orchestratorAgent;
        _queryAgent = queryAgent;
    _orchestrationService = orchestrationService;
        _openAIService = openAIService;
   _searchService = searchService;
        _logger = logger;
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
   url = request.Url,
   chunk_size = request.ChunkSize,
   chunk_overlap = request.ChunkOverlap,
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

          // Create execution context for query
     var context = _orchestrationService.CreateContext();
        context.State["query"] = request.Query;
     context.State["top_k"] = request.TopK;

   // Execute query
     var result = await _queryAgent.ExecuteAsync(context, cancellationToken);

   if (!result.Success)
   {
     _logger.LogError("Query processing failed for thread {ThreadId}: {Error}", context.ThreadId, result.Message);
    return StatusCode(500, CreateErrorResponse(result.Message, result.Errors, context.ThreadId));
       }

            var response = new
        {
       query = result.Data?.GetValueOrDefault("query", request.Query),
    answer = result.Data?.GetValueOrDefault("answer", ""),
       sources = result.Data?.GetValueOrDefault("sources", new List<object>()),
     source_count = result.Data?.GetValueOrDefault("source_count", 0),
    processing_time_ms = result.Data?.GetValueOrDefault("processing_time_ms", 0)
      };

  _logger.LogInformation("Query processed successfully for thread {ThreadId}", context.ThreadId);
     return Ok(response);
        }
        catch (Exception ex)
        {
 _logger.LogError(ex, "Unexpected error during query processing");
   return StatusCode(500, CreateErrorResponse("An unexpected error occurred during query processing"));
        }
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