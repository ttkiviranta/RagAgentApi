using RagAgentApi.Services;
using RagAgentApi.Services.GraphRag;
using System.Text;

namespace RagAgentApi.Services.GraphRag;

/// <summary>
/// Pipeline that merges graph retrieval results with vector search results and builds a unified prompt/context.
/// </summary>
public class GraphRagPipeline
{
    private readonly IAzureOpenAIService _openAIService;
    private readonly ILogger<GraphRagPipeline> _logger;

    public GraphRagPipeline(IAzureOpenAIService openAIService, ILogger<GraphRagPipeline> logger)
    {
        _openAIService = openAIService;
        _logger = logger;
    }

    /// <summary>
    /// Builds a unified context and a prompt by merging the vector search snippets and graph-based concepts/documents.
    /// </summary>
    public async Task<string> BuildPromptAsync(string query, List<string> vectorSnippets, GraphRetrievalResult graphResult, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Context for answering the user query:\n");

        if (graphResult?.Concepts?.Any() == true)
        {
            sb.AppendLine("Key concepts from knowledge graph:");
            foreach (var c in graphResult.Concepts.Take(8))
            {
                sb.AppendLine($"- {c.Name}: {c.Description?.Trim()}");
            }
            sb.AppendLine();
        }

        if (graphResult?.LinkedDocuments?.Any() == true)
        {
            sb.AppendLine("Linked documents from graph:");
            foreach (var d in graphResult.LinkedDocuments.Take(6))
            {
                sb.AppendLine($"- {d.Title ?? d.Url}: {d.Snippet?.Trim()}");
            }
            sb.AppendLine();
        }

        if (vectorSnippets?.Any() == true)
        {
            sb.AppendLine("Top vector-retrieved snippets:");
            foreach (var s in vectorSnippets.Take(6))
            {
                sb.AppendLine(s.Length > 400 ? s.Substring(0, 400) + "..." : s);
                sb.AppendLine();
            }
        }

        sb.AppendLine($"User question: {query}");

        // Add short instruction for the LLM to use the context
        sb.AppendLine();
        sb.AppendLine("Instruction: Use the context above to answer the user's question. When referencing sources, prefer the linked documents. If information is insufficient, say you don't know and suggest clarifying questions.");

        // Optionally refine prompt via LLM summarization (not required)
        return sb.ToString();
    }
}
