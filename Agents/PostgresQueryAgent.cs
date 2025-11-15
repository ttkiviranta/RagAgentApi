using RagAgentApi.Models;
using RagAgentApi.Services;

namespace RagAgentApi.Agents;

/// <summary>
/// PostgreSQL-based query agent for RAG system
/// </summary>
public class PostgresQueryAgent : BaseRagAgent
{
    private readonly PostgresQueryService _queryService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly ConversationService _conversationService;
    private readonly IConfiguration _configuration;

    public PostgresQueryAgent(
        PostgresQueryService queryService,
      IAzureOpenAIService openAIService,
        ConversationService conversationService,
      IConfiguration configuration,
        ILogger<PostgresQueryAgent> logger) : base(logger)
    {
     _queryService = queryService;
        _openAIService = openAIService;
        _conversationService = conversationService;
        _configuration = configuration;
    }

    public override string Name => "PostgresQueryAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
   LogExecutionStart(context.ThreadId);

        try
        {
            // Validate required data in context
       if (!context.State.TryGetValue("query", out var queryObj) || queryObj is not string query)
        {
        return AgentResult.CreateFailure("Query not found in context state");
        }

            var topK = GetTopK(context);
            var minScore = GetMinScore(context);

     _logger.LogInformation("[PostgresQueryAgent] Processing query: '{Query}' with top-K {TopK}, min-score {MinScore}", 
   query, topK, minScore);

      // Step 1: Generate query embedding
            var queryEmbedding = await _openAIService.GetEmbeddingAsync(query, cancellationToken);

    // Step 2: Search for relevant documents using PostgreSQL
            var searchResults = await _queryService.SearchAsync(queryEmbedding, topK, minScore, cancellationToken);

            if (!searchResults.Any())
    {
                _logger.LogWarning("[PostgresQueryAgent] No relevant documents found for query: '{Query}'", query);

        // Check for similar past queries
      var similarQueries = await _queryService.FindSimilarQueriesAsync(queryEmbedding, 3, 0.8, cancellationToken);
    var noResultsAnswer = similarQueries.Any() 
       ? $"I couldn't find relevant information for your question. However, I found similar questions that might help. You could also try rephrasing your query."
  : "I couldn't find any relevant information to answer your question. Please try rephrasing your query or check if the content has been ingested into the system.";

          return AgentResult.CreateSuccess(
 "Query processed (no results)",
     new Dictionary<string, object>
        {
       { "query", query },
           { "answer", noResultsAnswer },
   { "sources", new List<object>() },
        { "similar_queries", similarQueries.Take(3).Select(sq => new { sq.Query, sq.Answer, sq.SimilarityScore }).ToList() },
          { "source_count", 0 }
        });
            }

            _logger.LogDebug("[PostgresQueryAgent] Found {Count} search results", searchResults.Count);
      foreach (var result in searchResults.Take(3))
     {
      _logger.LogDebug("[PostgresQueryAgent] Result: Score={Score:F3}, URL='{URL}', Content='{Content}'", 
       result.RelevanceScore, 
       result.SourceUrl,
         result.Content.Length > 50 ? result.Content.Substring(0, 50) + "..." : result.Content);
         }

         // Step 3: Build context from retrieved documents
  var contextParts = searchResults.Select(r => r.Content).ToList();
       var contextText = string.Join("\n\n", contextParts);

            // Step 4: Generate answer using RAG prompt
   var systemPrompt = BuildSystemPrompt();
      var userPrompt = BuildUserPrompt(contextText, query);

      var answer = await _openAIService.GetChatCompletionAsync(systemPrompt, userPrompt, cancellationToken);

       // Step 5: Prepare sources information
          var sources = searchResults.Select(r => new
            {
      content = r.Content.Length > 200 ? r.Content.Substring(0, 200) + "..." : r.Content,
     source_url = r.SourceUrl,
     chunk_index = r.ChunkIndex,
             score = Math.Round(r.RelevanceScore, 3),
                created_at = r.CreatedAt,
      document_title = r.Metadata.DocumentTitle,
chunk_id = r.ChunkId
            }).ToList();

            // Step 6: Save conversation (optional)
       var conversationId = await CreateOrUpdateConversationAsync(context, query, answer, queryEmbedding, sources.Cast<object>().ToList(), cancellationToken);

        // Store result in context
context.State["query_result"] = new
            {
                query = query,
      answer = answer,
             sources = sources,
         source_count = sources.Count,
    conversation_id = conversationId
          };

          stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

            _logger.LogInformation("[PostgresQueryAgent] Retrieved {Count} docs, generated answer for query: '{Query}'", 
     searchResults.Count, query);

         AddMessage(context, "System", "Query processed successfully using PostgreSQL",
    new Dictionary<string, object>
  {
          { "query", query },
   { "sources_found", sources.Count },
              { "answer_length", answer.Length },
    { "conversation_id", conversationId }
            });

 return AgentResult.CreateSuccess(
             "Query processed successfully",
                new Dictionary<string, object>
 {
        { "query", query },
           { "answer", answer },
     { "sources", sources },
            { "source_count", sources.Count },
           { "conversation_id", conversationId },
{ "processing_time_ms", stopwatch.ElapsedMilliseconds }
     });
   }
        catch (Exception ex)
        {
            stopwatch.Stop();
   LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
 return HandleException(ex, context.ThreadId, "PostgreSQL query processing");
        }
}

    private async Task<Guid?> CreateOrUpdateConversationAsync(
        AgentContext context, 
        string query, 
        string answer, 
        float[] queryEmbedding, 
        List<object> sources, 
  CancellationToken cancellationToken)
    {
        try
        {
   // Create new conversation if not exists
            var conversation = await _conversationService.CreateConversationAsync(
                userId: context.State.TryGetValue("user_id", out var userIdObj) ? userIdObj?.ToString() : null,
   title: null, // Will be auto-generated from first user message
                cancellationToken: cancellationToken);

 // Add user message
          await _conversationService.AddMessageAsync(
  conversation.Id,
        "user",
    query,
             queryEmbedding,
    cancellationToken: cancellationToken);

        // Add assistant response
            await _conversationService.AddMessageAsync(
    conversation.Id,
          "assistant", 
             answer,
  sources: sources,
                model: "gpt-35-turbo", // From config
      cancellationToken: cancellationToken);

  return conversation.Id;
        }
        catch (Exception ex)
   {
          _logger.LogWarning(ex, "[PostgresQueryAgent] Failed to save conversation, continuing without it");
    return null;
        }
    }

    private int GetTopK(AgentContext context)
    {
      if (context.State.TryGetValue("top_k", out var topKObj) && topKObj is int topK)
      {
            return Math.Min(topK, _configuration.GetValue<int>("RagSettings:MaxTopK", 50));
        }

        return _configuration.GetValue<int>("RagSettings:DefaultTopK", 5);
    }

    private double GetMinScore(AgentContext context)
 {
        if (context.State.TryGetValue("min_score", out var minScoreObj) && minScoreObj is double minScore)
        {
            return minScore;
        }

        return _configuration.GetValue<double>("RagSettings:MinimumSearchScore", 0.5);
    }

    private string BuildSystemPrompt()
    {
        return @"You are a helpful AI assistant that answers questions based on the provided context using a PostgreSQL vector database.

Follow these guidelines:
- Answer questions based ONLY on the information provided in the context
- If the context doesn't contain enough information to answer the question, clearly state this
- Be concise but thorough in your responses
- If asked about something not covered in the context, explain that you don't have that information
- Maintain a professional and helpful tone
- Do not make up information that isn't in the context
- If you find relevant information, cite the sources by mentioning the source URLs when helpful";
    }

    private string BuildUserPrompt(string context, string query)
    {
        return $@"Context from documents:
{context}

Question: {query}

Answer:";
    }
}