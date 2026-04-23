using RagAgentApi.Models;
using RagAgentApi.Services;

namespace RagAgentApi.Agents;

/// <summary>
/// Queries the RAG system and generates answers using retrieved context
/// </summary>
public class QueryAgent : BaseRagAgent
{
  private readonly IAzureSearchService _searchService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly IConfiguration _configuration;

    public QueryAgent(
        IAzureSearchService searchService,
        IAzureOpenAIService openAIService,
        IConfiguration configuration,
        ILogger<QueryAgent> logger,
        IErrorLogService? errorLogService = null) : base(logger, errorLogService)
    {
        _searchService = searchService;
        _openAIService = openAIService;
        _configuration = configuration;
    }

    public override string Name => "QueryAgent";

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

       _logger.LogInformation("[QueryAgent] Processing query: '{Query}' with top-K {TopK}", query, topK);

            // Step 1: Generate query embedding
       var queryEmbedding = await _openAIService.GetEmbeddingAsync(query, cancellationToken);

            // Step 2: Search for relevant documents
            var searchResults = await _searchService.SearchAsync(queryEmbedding, topK, cancellationToken);

     if (!searchResults.Any())
   {
_logger.LogWarning("[QueryAgent] No relevant documents found for query: '{Query}'", query);
  
     var noResultsAnswer = "I couldn't find any relevant information to answer your question. Please try rephrasing your query or check if the content has been ingested into the system.";
       
    return AgentResult.CreateSuccess(
              "Query processed (no results)",
        new Dictionary<string, object>
     {
           { "query", query },
             { "answer", noResultsAnswer },
 { "sources", new List<object>() },
    { "source_count", 0 }
        });
    }

   // Debug logging for search results
            _logger.LogDebug("[QueryAgent] Found {Count} search results", searchResults.Count);
            foreach (var result in searchResults.Take(3))
       {
       _logger.LogDebug("[QueryAgent] Result: Score={Score}, Content='{Content}', URL='{URL}'", 
      result.Score, 
          result.Document?.Content?.Substring(0, Math.Min(result.Document.Content?.Length ?? 0, 50)) ?? "null",
          result.Document?.SourceUrl ?? "null");
  }

       // Step 3: Filter results by minimum score
            var minScore = _configuration.GetValue<double>("RagSettings:MinimumSearchScore", 0.5);
 var filteredResults = searchResults.Where(r => r.Score >= minScore).ToList();

            if (!filteredResults.Any())
      {
           _logger.LogWarning("[QueryAgent] No documents met minimum score threshold {MinScore} for query: '{Query}'", 
       minScore, query);
       
        var lowScoreAnswer = "I found some potentially relevant information, but it doesn't seem closely related to your question. Please try rephrasing your query for better results.";
        
         return AgentResult.CreateSuccess(
 "Query processed (low relevance)",
         new Dictionary<string, object>
       {
     { "query", query },
    { "answer", lowScoreAnswer },
        { "sources", new List<object>() },
           { "source_count", 0 }
   });
 }

            // Step 4: Build context from retrieved documents
            var contextParts = filteredResults.Select(r => r.Document.Content).ToList();
        var contextText = string.Join("\n\n", contextParts);

            // Step 5: Generate answer using RAG prompt
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(contextText, query);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var answer = await _openAIService.GetChatCompletionAsync(systemPrompt, userPrompt, cancellationToken);
            sw.Stop();

            // Telemetry: report LLM call latency and token counts if available in context
            try
            {
                var telemetry = HttpContextAccessorHolder.Accessor?.HttpContext?.RequestServices.GetService(typeof(ITelemetryService)) as ITelemetryService;
                if (telemetry != null)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "selected_agent", Name },
                        { "retrieval_mode", context.State.GetValueOrDefault("retrieval_mode")?.ToString() ?? "Rag" },
                        { "query", query.Length > 100 ? query.Substring(0, 100) : query }
                    };
                    telemetry.TrackMetric("llm_call_latency_ms", sw.ElapsedMilliseconds, props);
                }
            }
            catch { }

      // Step 6: Prepare sources information
        var sources = filteredResults.Select(r => new
            {
content = r.Document.Content?.Length > 200 ? r.Document.Content.Substring(0, 200) + "..." : r.Document.Content ?? "",
        source_url = r.Document.SourceUrl ?? "",
          chunk_index = r.Document.ChunkIndex,
            score = Math.Round(r.Score ?? 0, 3),
     created_at = r.Document.CreatedAt
       }).ToList();

          // Store result in context
          context.State["query_result"] = new
            {
        query = query,
      answer = answer,
              sources = sources,
  source_count = sources.Count
            };

        stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

       _logger.LogInformation("[QueryAgent] Retrieved {Count} docs, generated answer for query: '{Query}'", 
   filteredResults.Count, query);

            AddMessage(context, "System", "Query processed successfully",
 new Dictionary<string, object>
 {
         { "query", query },
       { "sources_found", sources.Count },
             { "answer_length", answer.Length }
          });

            return AgentResult.CreateSuccess(
   "Query processed successfully",
                new Dictionary<string, object>
         {
             { "query", query },
  { "answer", answer },
        { "sources", sources },
    { "source_count", sources.Count },
      { "processing_time_ms", stopwatch.ElapsedMilliseconds }
              });
        }
        catch (Exception ex)
 {
            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
     return HandleException(ex, context.ThreadId, "query processing");
  }
    }

    private int GetTopK(AgentContext context)
    {
        if (context.State.TryGetValue("top_k", out var topKObj) && topKObj is int topK)
        {
            return topK;
        }

        return _configuration.GetValue<int>("RagSettings:DefaultTopK", 5);
    }

    private string BuildSystemPrompt()
    {
        return @"You are a helpful AI assistant that answers questions based on the provided context.

IMPORTANT: Format your responses using Markdown for better readability:
- Use **bold** for important terms
- Use proper headings (##, ###) to structure your answer
- Use bullet points (-) or numbered lists for multiple items
- Use backticks (`) for code or technical terms
- Use > for important notes or quotes
- Keep paragraphs short and clear

Follow these guidelines:
- Answer questions based ONLY on the information provided in the context
- If the context doesn't contain enough information to answer the question, clearly state this
- Be concise but thorough in your responses
- If asked about something not covered in the context, explain that you don't have that information
- Maintain a professional and helpful tone
- Do not make up information that isn't in the context
- Always format your final answer using Markdown";
    }

    private string BuildUserPrompt(string context, string query)
    {
        // Detect language and format accordingly
        // If query contains Finnish characters, respond in Finnish
        bool isFinnish = ContainsFinnishCharacters(query);

        if (isFinnish)
        {
            return $@"Konteksti:
{context}

Kysymys: {query}

Vastaa hyvin muotoillulla Markdown-vastuksella:";
        }
        else
        {
            return $@"Context:
{context}

Question: {query}

Please provide a well-formatted Markdown answer:";
        }
    }

    private bool ContainsFinnishCharacters(string text)
    {
        // Check for Finnish-specific characters: ä, ö, å
        return text.Contains('ä') || text.Contains('ö') || text.Contains('å') ||
               text.Contains('Ä') || text.Contains('Ö') || text.Contains('Å');
    }
}