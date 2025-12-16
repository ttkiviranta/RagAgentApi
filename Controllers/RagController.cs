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
    private readonly IConfiguration _configuration;

    public RagController(
        OrchestratorAgent orchestratorAgent,
        QueryAgent queryAgent,
        AgentOrchestrationService orchestrationService,
        IAzureOpenAIService openAIService,
        IAzureSearchService searchService,
        ILogger<RagController> logger,
        IConfiguration configuration)
    {
        _orchestratorAgent = orchestratorAgent;
        _queryAgent = queryAgent;
        _orchestrationService = orchestrationService;
        _openAIService = openAIService;
        _searchService = searchService;
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

            // Get RAG mode configuration
            var ragMode = _configuration.GetValue<string>("RagSettings:Mode", "hybrid")?.ToLower();
            
            // Get embedding for query
            var queryEmbedding = await _openAIService.GetEmbeddingAsync(request.Query, cancellationToken);
            
            // Use PostgresQueryService for vector search
            var postgresQueryService = HttpContext.RequestServices.GetRequiredService<PostgresQueryService>();
            var searchResults = await postgresQueryService.SearchAsync(queryEmbedding, request.TopK, 0.5, cancellationToken);

            string answer;
            List<object> sources;

            if (!searchResults.Any())
            {
                _logger.LogWarning("[RagController] No documents found for query: '{Query}'", request.Query);
                
                if (ragMode == "strict")
                {
                    // Strict mode: error response
                    answer = "Kontekstissa ei ole tietoa tähän kysymykseen. " +
                            "Varmista että olet ensin ladannut dokumentteja järjestelmään käyttämällä 'Ingest Document' -toimintoa.";
                    sources = new List<object>();
                }
                else
                {
                    // Hybrid mode: use general ChatGPT knowledge
                    var systemPrompt = @"You are a helpful AI assistant. Answer the user's question based on your general knowledge.
Be concise, accurate, and helpful. If you're not certain about something, clearly state your level of confidence.
Provide practical and useful information.";
                    
                    answer = "?? Dokumenteista ei löytynyt tietoa. Vastaan yleisen tietämykseni perusteella:\n\n" +
                            await _openAIService.GetChatCompletionAsync(systemPrompt, request.Query, cancellationToken);
                    
                    sources = new List<object>();
                    
                    _logger.LogInformation("[RagController] Generated answer using general knowledge (no context)");
                }
            }
            else
            {
                // Documents found - use RAG
                var context = string.Join("\n\n", searchResults.Select(r => r.Content));
                
                var systemPrompt = @"You are a helpful AI assistant that answers questions based ONLY on the provided context.

IMPORTANT RULES:
- Answer ONLY using information from the context below
- If the context doesn't contain the answer, clearly state that you don't have that information
- Do NOT use your general knowledge to answer questions
- Be concise and cite specific parts of the context when relevant

Context:
" + context;

                answer = "?? Vastaus dokumenttien perusteella:\n\n" +
                        await _openAIService.GetChatCompletionAsync(systemPrompt, request.Query, cancellationToken);
                
                sources = searchResults.Select(r => new
                {
                    url = r.SourceUrl ?? "",
                    content = r.Content.Length > 200 ? r.Content.Substring(0, 200) + "..." : r.Content,
                    relevanceScore = Math.Round(r.RelevanceScore, 3)
                }).ToList<object>();
                
                _logger.LogInformation("[RagController] Generated answer with {SourceCount} sources", sources.Count);
            }

            var response = new
            {
                query = request.Query,
                answer = answer,
                sources = sources,
                source_count = sources.Count,
                processing_time_ms = 0 // Would need stopwatch
            };

            _logger.LogInformation("Query processed successfully");
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

            _logger.LogInformation("Starting enhanced content ingestion for URL: {Url}", request.Url);

            // Create new execution context
            var context = _orchestrationService.CreateContext();

            // Set initial state
            context.State["url"] = request.Url;
            context.State["chunk_size"] = request.ChunkSize;
            context.State["chunk_overlap"] = request.ChunkOverlap;
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