using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Agents;
using RagAgentApi.Services;
using RagAgentApi.Models;
using System.Text.Json;

namespace RagAgentApi.Controllers;

/// <summary>
/// Test controller for PostgreSQL-based RAG services
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PostgresTestController : ControllerBase
{
    private readonly PostgresStorageAgent _storageAgent;
    private readonly PostgresQueryAgent _queryAgent;
    private readonly PostgresQueryService _queryService;
    private readonly ConversationService _conversationService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly AgentOrchestrationService _orchestrationService;
    private readonly ILogger<PostgresTestController> _logger;

    public PostgresTestController(
        PostgresStorageAgent storageAgent,
        PostgresQueryAgent queryAgent,
  PostgresQueryService queryService,
        ConversationService conversationService,
        IAzureOpenAIService openAIService,
        AgentOrchestrationService orchestrationService,
  ILogger<PostgresTestController> logger)
    {
     _storageAgent = storageAgent;
        _queryAgent = queryAgent;
        _queryService = queryService;
        _conversationService = conversationService;
        _openAIService = openAIService;
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    /// <summary>
    /// Test full PostgreSQL RAG pipeline with sample data
    /// </summary>
    [HttpPost("full-pipeline")]
    public async Task<IActionResult> TestFullPipeline()
    {
        try
        {
 _logger.LogInformation("Starting PostgreSQL RAG pipeline test");

   // Sample data
     var testUrl = "https://test.example.com/sample-doc";
            var testContent = @"PostgreSQL is a powerful, open source object-relational database system with over 35 years of active development. 
           It has earned a strong reputation for reliability, feature robustness, and performance.
            PostgreSQL supports both SQL and NoSQL queries. 
 The pgvector extension adds vector similarity search capabilities to PostgreSQL.
      This makes PostgreSQL suitable for AI and machine learning applications.";

    var testChunks = new List<string>
 {
                "PostgreSQL is a powerful, open source object-relational database system with over 35 years of active development.",
   "It has earned a strong reputation for reliability, feature robustness, and performance. PostgreSQL supports both SQL and NoSQL queries.",
      "The pgvector extension adds vector similarity search capabilities to PostgreSQL. This makes PostgreSQL suitable for AI and machine learning applications."
     };

            // Step 1: Generate embeddings for chunks
            var embeddings = new List<float[]>();
    foreach (var chunk in testChunks)
 {
       var embedding = await _openAIService.GetEmbeddingAsync(chunk);
      embeddings.Add(embedding);
     }

         // Step 2: Create context and store data
        var context = _orchestrationService.CreateContext();
     context.State["url"] = testUrl;
            context.State["raw_content"] = testContent;
            context.State["chunks"] = testChunks;
    context.State["embeddings"] = embeddings;

          var storageResult = await _storageAgent.ExecuteAsync(context);

   if (!storageResult.Success)
            {
                return StatusCode(500, new { error = "Storage failed", details = storageResult.Message });
        }

          // Step 3: Test query
            var testQuery = "What is PostgreSQL used for?";
            var queryContext = _orchestrationService.CreateContext();
            queryContext.State["query"] = testQuery;
     queryContext.State["top_k"] = 3;

 var queryResult = await _queryAgent.ExecuteAsync(queryContext);

            if (!queryResult.Success)
 {
      return StatusCode(500, new { error = "Query failed", details = queryResult.Message });
      }

            // Step 4: Get stats
   var searchStats = await _queryService.GetSearchStatsAsync();
         var conversationStats = await _conversationService.GetStatsAsync();

  var result = new
 {
          status = "success",
         storage_result = new
    {
         document_id = storageResult.Data?.GetValueOrDefault("document_id"),
           chunks_stored = storageResult.Data?.GetValueOrDefault("chunks_stored"),
          storage_time_ms = storageResult.Data?.GetValueOrDefault("storage_time_ms")
     },
 query_result = new
        {
query = queryResult.Data?.GetValueOrDefault("query"),
    answer = queryResult.Data?.GetValueOrDefault("answer"),
      source_count = queryResult.Data?.GetValueOrDefault("source_count"),
        conversation_id = queryResult.Data?.GetValueOrDefault("conversation_id"),
              processing_time_ms = queryResult.Data?.GetValueOrDefault("processing_time_ms")
     },
      database_stats = new
    {
         search_stats = searchStats,
     conversation_stats = conversationStats
                },
      timestamp = DateTimeOffset.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostgreSQL pipeline test failed");
            return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
    /// Test PostgreSQL query service directly
    /// </summary>
    [HttpPost("query-service")]
    public async Task<IActionResult> TestQueryService([FromBody] TestQueryRequest request)
    {
        try
        {
    var queryEmbedding = await _openAIService.GetEmbeddingAsync(request.Query);

            var searchResults = await _queryService.SearchAsync(
  queryEmbedding, 
        request.TopK, 
 request.MinScore);

var similarQueries = await _queryService.FindSimilarQueriesAsync(
              queryEmbedding, 
 5, 
  0.7);

   var result = new
         {
      query = request.Query,
      search_results = searchResults.Select(r => new
         {
                  chunk_id = r.ChunkId,
      content = r.Content.Length > 200 ? r.Content.Substring(0, 200) + "..." : r.Content,
         source_url = r.SourceUrl,
         chunk_index = r.ChunkIndex,
     relevance_score = Math.Round(r.RelevanceScore, 3),
  created_at = r.CreatedAt
             }).ToList(),
          similar_queries = similarQueries.Select(sq => new
                {
      query = sq.Query,
   answer = sq.Answer,
        similarity_score = Math.Round(sq.SimilarityScore, 3),
     created_at = sq.CreatedAt
      }).ToList(),
    search_stats = await _queryService.GetSearchStatsAsync(),
      timestamp = DateTimeOffset.UtcNow
     };

   return Ok(result);
        }
catch (Exception ex)
      {
          _logger.LogError(ex, "Query service test failed");
  return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
    /// Test conversation service
 /// </summary>
    [HttpPost("conversation")]
    public async Task<IActionResult> TestConversationService()
    {
        try
        {
   // Create test conversation
      var conversation = await _conversationService.CreateConversationAsync(
       userId: "test-user",
 title: "Test Conversation");

      // Add test messages
        var userMessage = await _conversationService.AddMessageAsync(
       conversation.Id,
              "user",
             "What is PostgreSQL?");

            var assistantMessage = await _conversationService.AddMessageAsync(
    conversation.Id,
       "assistant",
       "PostgreSQL is a powerful open-source relational database system.",
   model: "gpt-35-turbo");

      // Get conversation history
            var history = await _conversationService.GetConversationHistoryAsync(conversation.Id);

   // Search conversations
         var searchResults = await _conversationService.SearchConversationsAsync("PostgreSQL");

         var result = new
            {
                conversation_id = conversation.Id,
     messages = history?.Messages.Select(m => new
     {
       id = m.Id,
  role = m.Role,
         content = m.Content,
     created_at = m.CreatedAt
    }).ToList(),
      search_results = searchResults.Select(sr => new
      {
             conversation_id = sr.Conversation.Id,
        title = sr.Conversation.Title,
              match_type = sr.MatchType,
        matching_content = sr.MatchingMessage.Content.Length > 100 
 ? sr.MatchingMessage.Content.Substring(0, 100) + "..." 
       : sr.MatchingMessage.Content
                }).ToList(),
                stats = await _conversationService.GetStatsAsync(),
        timestamp = DateTimeOffset.UtcNow
            };

     return Ok(result);
        }
 catch (Exception ex)
  {
        _logger.LogError(ex, "Conversation service test failed");
            return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
 /// Compare PostgreSQL vs Azure Search performance
    /// </summary>
    [HttpPost("performance-comparison")]
    public async Task<IActionResult> ComparePerformance([FromBody] TestQueryRequest request)
    {
        try
     {
var queryEmbedding = await _openAIService.GetEmbeddingAsync(request.Query);
        
    // Test PostgreSQL
            var postgresStopwatch = System.Diagnostics.Stopwatch.StartNew();
     var postgresResults = await _queryService.SearchAsync(queryEmbedding, request.TopK);
            postgresStopwatch.Stop();

            // Note: Azure Search comparison would need the original AzureSearchService
  // For now, we'll just show PostgreSQL performance

            var result = new
  {
                query = request.Query,
                postgres_performance = new
       {
 search_time_ms = postgresStopwatch.ElapsedMilliseconds,
      results_count = postgresResults.Count,
 avg_relevance_score = postgresResults.Any() 
            ? Math.Round(postgresResults.Average(r => r.RelevanceScore), 3) 
         : 0,
               top_score = postgresResults.Any() 
     ? Math.Round(postgresResults.Max(r => r.RelevanceScore), 3) 
              : 0
        },
       database_stats = await _queryService.GetSearchStatsAsync(),
 timestamp = DateTimeOffset.UtcNow
            };

 return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Performance comparison test failed");
            return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }
}

/// <summary>
/// Test query request
/// </summary>
public class TestQueryRequest
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.5;
}