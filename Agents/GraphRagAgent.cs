using RagAgentApi.Models;
using RagAgentApi.Services;
using RagAgentApi.Services.GraphRag;

namespace RagAgentApi.Agents;

/// <summary>
/// GraphRAG agent that combines graph-based retrieval with existing vector search.
/// </summary>
public class GraphRagAgent : BaseRagAgent
{
    private readonly GraphRetriever _graphRetriever;
    private readonly GraphRagPipeline _pipeline;
    private readonly PostgresQueryService _queryService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly ConversationService _conversationService;
    private readonly IConfiguration _configuration;

    public GraphRagAgent(
        GraphRetriever graphRetriever,
        GraphRagPipeline pipeline,
        PostgresQueryService queryService,
        IAzureOpenAIService openAIService,
        ConversationService conversationService,
        IConfiguration configuration,
        ILogger<GraphRagAgent> logger,
        IErrorLogService? errorLogService = null) : base(logger, errorLogService)
    {
        _graphRetriever = graphRetriever;
        _pipeline = pipeline;
        _queryService = queryService;
        _openAIService = openAIService;
        _conversationService = conversationService;
        _configuration = configuration;
    }

    public override string Name => "GraphRagAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

        try
        {
            if (!context.State.TryGetValue("query", out var qObj) || qObj is not string query)
                return AgentResult.CreateFailure("Query not found in context state");

            var topK = 5;
            if (context.State.TryGetValue("top_k", out var topKObj) && topKObj is int k)
                topK = k;

            _logger.LogInformation("[GraphRagAgent] Executing GraphRAG for query: {Query}", query);

            // Step 1: Vector search
            var embedding = await _openAIService.GetEmbeddingAsync(query, cancellationToken);
            var vectorResults = await _queryService.SearchAsync(embedding, topK, 0.0, cancellationToken);
            var vectorSnippets = vectorResults.Select(r => r.Content).ToList();

            // Step 2: Graph retrieval
            var graphResult = await _graphRetriever.ExtractAndTraverseAsync(query, depth: 2, cancellationToken: cancellationToken);

            // Step 3: Merge in pipeline and build prompt
            var prompt = await _pipeline.BuildPromptAsync(query, vectorSnippets, graphResult, cancellationToken);

            // Step 4: Generate final answer
            var systemPrompt = "You are a helpful assistant that uses provided context to answer user questions. Provide concise answers and cite sources.";

            var answer = await _openAIService.GetChatCompletionAsync(systemPrompt, prompt, cancellationToken);

            // Build sources list
            var sources = new List<object>();
            sources.AddRange(vectorResults.Select(r => new
            {
                source = r.SourceUrl,
                snippet = r.Content.Length > 200 ? r.Content.Substring(0, 200) + "..." : r.Content,
                score = Math.Round(r.RelevanceScore, 3)
            }));

            sources.AddRange(graphResult.LinkedDocuments.Select(d => new
            {
                source = d.Url,
                title = d.Title,
                snippet = d.Snippet
            }));

            // Save to conversation
            var conversationId = await CreateOrUpdateConversationAsync(context, query, answer, embedding, sources.Cast<object>().ToList(), cancellationToken);

            var resultObj = new
            {
                query = query,
                answer = answer,
                sources = sources,
                conversation_id = conversationId
            };

            context.State["query_result_graph"] = resultObj;

            sw.Stop();
            LogExecutionComplete(context.ThreadId, true, sw.Elapsed);

            AddMessage(context, "System", "GraphRAG query processed", new Dictionary<string, object>
            {
                { "query", query },
                { "source_count", sources.Count }
            });

            return AgentResult.CreateSuccess("GraphRAG processed successfully", new Dictionary<string, object>
            {
                { "result", resultObj }
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogExecutionComplete(context.ThreadId, false, sw.Elapsed);
            return HandleException(ex, context.ThreadId, "GraphRAG execution");
        }
    }

    private async Task<Guid?> CreateOrUpdateConversationAsync(AgentContext context, string query, string answer, float[] queryEmbedding, List<object> sources, CancellationToken cancellationToken)
    {
        try
        {
            var conversation = await _conversationService.CreateConversationAsync(
                userId: context.State.TryGetValue("user_id", out var uid) ? uid?.ToString() : null,
                title: null,
                cancellationToken: cancellationToken);

            await _conversationService.AddMessageAsync(conversation.Id, "user", query, null, cancellationToken);
            await _conversationService.AddMessageAsync(conversation.Id, "assistant", answer, null, cancellationToken);

            return conversation.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create conversation for GraphRAG result");
            return null;
        }
    }
}
