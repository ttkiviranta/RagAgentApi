using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace RagAgentApi.Services.Retrieval;

/// <summary>
/// RAG (Retrieval-Augmented Generation) strategy.
/// Uses vector similarity search to find relevant document chunks and generates answers.
/// Best for large document collections where semantic search is needed.
/// </summary>
public class RagRetrievalStrategy : IRetrievalStrategy
{
    private readonly PostgresQueryService _queryService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly ILogger<RagRetrievalStrategy> _logger;
    private readonly RetrievalSettings _settings;

    public string Name => "Rag";

    public RagRetrievalStrategy(
        PostgresQueryService queryService,
        IAzureOpenAIService openAIService,
        IOptions<RetrievalSettings> settings,
        ILogger<RagRetrievalStrategy> logger)
    {
        _queryService = queryService;
        _openAIService = openAIService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RetrievalResult> ExecuteAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new RetrievalResult
        {
            StrategyUsed = Name,
            ConfiguredMode = "Rag"
        };

        try
        {
            _logger.LogInformation("[RagStrategy] Executing RAG retrieval for query: '{Query}'", query);

            // Generate embedding for the query
            var queryEmbedding = await _openAIService.GetEmbeddingAsync(query, cancellationToken);

            // Search for relevant documents using vector similarity
            var searchResults = await _queryService.SearchAsync(
                queryEmbedding, 
                topK, 
                _settings.MinimumRelevanceScore, 
                cancellationToken);

            if (!searchResults.Any())
            {
                _logger.LogWarning("[RagStrategy] No documents found for query");
                result.Answer = "Dokumenteista ei löytynyt tietoa tähän kysymykseen.";
                result.Success = true;
                result.Metadata["no_results"] = true;
            }
            else
            {
                // Build context from search results
                var context = string.Join("\n\n", searchResults.Select(r => r.Content));

                var systemPrompt = @"You are a helpful AI assistant that answers questions based ONLY on the provided context.

IMPORTANT RULES:
- Answer ONLY using information from the context below
- If the context doesn't contain the answer, clearly state that you don't have that information
- Do NOT use your general knowledge to answer questions
- Be concise and cite specific parts of the context when relevant

Context:
" + context;

                var answer = await _openAIService.GetChatCompletionAsync(systemPrompt, query, cancellationToken);
                result.Answer = "📚 Vastaus dokumenttien perusteella:\n\n" + answer;

                result.Sources = searchResults.Select(r => new RetrievalSource
                {
                    Url = r.SourceUrl ?? "",
                    Content = r.Content.Length > 200 ? r.Content.Substring(0, 200) + "..." : r.Content,
                    RelevanceScore = Math.Round(r.RelevanceScore, 3)
                }).ToList();

                result.Success = true;
                result.Metadata["chunks_used"] = searchResults.Count();

                _logger.LogInformation("[RagStrategy] Generated answer using {SourceCount} sources", result.Sources.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RagStrategy] Error during retrieval");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        stopwatch.Stop();
        result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        return result;
    }
}
