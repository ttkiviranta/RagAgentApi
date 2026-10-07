using Microsoft.EntityFrameworkCore;
using RagAgentApi.Data;
using RagAgentApi.Models.PostgreSQL;

namespace RagAgentApi.Services.GraphRag;

/// <summary>
/// Retrieves concepts and related documents from the knowledge graph.
/// Uses an LLM to extract seed concepts from a user question and then traverses relations.
/// </summary>
public class GraphRetriever
{
    private readonly RagDbContext _db;
    private readonly IAzureOpenAIService _openAIService;
    private readonly PostgresQueryService _queryService;
    private readonly ILogger<GraphRetriever> _logger;

    public GraphRetriever(
        RagDbContext db,
        IAzureOpenAIService openAIService,
        PostgresQueryService queryService,
        ILogger<GraphRetriever> logger)
    {
        _db = db;
        _openAIService = openAIService;
        _queryService = queryService;
        _logger = logger;
    }

    public async Task<GraphRetrievalResult> ExtractAndTraverseAsync(string question, int depth = 2, CancellationToken cancellationToken = default)
    {
        // Step 1: Ask LLM to extract concise concept keywords
        var system = "You are an extractor that returns a short comma-separated list of concepts from a user's question.";
        var prompt = $"Extract the 3-6 most important concepts from the following user question. Return only a comma separated list of short concept names (no explanation):\n\n{question}";

        var response = await _openAIService.GetChatCompletionAsync(system, prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(response))
        {
            _logger.LogWarning("[GraphRetriever] LLM returned empty concept list for question: {Question}", question);
            return new GraphRetrievalResult();
        }

        var conceptNames = response
            .Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation("[GraphRetriever] Seed concepts: {Concepts}", string.Join(",", conceptNames));

        // Step 2: Find matching concepts in DB
        var matchedConcepts = await _db.Concepts
            .Where(c => conceptNames.Contains(c.Name))
            .ToListAsync(cancellationToken);

        // Case-insensitive fallback search if exact matches not found
        if (!matchedConcepts.Any())
        {
            matchedConcepts = await _db.Concepts
                .Where(c => conceptNames.Any(n => EF.Functions.ILike(c.Name, $"%{n}%")))
                .ToListAsync(cancellationToken);
        }

        var result = new GraphRetrievalResult();
        var visited = new HashSet<Guid>();
        var frontier = new Queue<(Concept concept, int level)>();

        foreach (var c in matchedConcepts)
        {
            frontier.Enqueue((c, 0));
            visited.Add(c.Id);
            result.Concepts.Add(c);
        }

        // Step 3: BFS traversal up to depth
        while (frontier.Any())
        {
            var (current, level) = frontier.Dequeue();
            if (level >= depth) continue;

            var outgoing = await _db.Relations
                .Include(r => r.TargetConcept)
                .Include(r => r.Document)
                .Where(r => r.SourceConceptId == current.Id)
                .ToListAsync(cancellationToken);

            foreach (var rel in outgoing)
            {
                if (rel.TargetConcept != null && !visited.Contains(rel.TargetConcept.Id))
                {
                    visited.Add(rel.TargetConcept.Id);
                    frontier.Enqueue((rel.TargetConcept, level + 1));
                    result.Concepts.Add(rel.TargetConcept);
                }

                if (rel.DocumentId.HasValue)
                {
                    try
                    {
                        var doc = await _db.Documents.FindAsync(new object[] { rel.DocumentId.Value }, cancellationToken);
                        if (doc != null)
                        {
                            result.LinkedDocuments.Add(new GraphLinkedDocument
                            {
                                DocumentId = doc.Id,
                                Url = doc.Url,
                                Title = doc.Title,
                                Snippet = doc.Content?.Length > 500 ? doc.Content.Substring(0, 500) + "..." : doc.Content
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load document {DocumentId} referenced from relation", rel.DocumentId);
                    }
                }
            }

            // Also check incoming relations to capture reciprocal edges
            var incoming = await _db.Relations
                .Include(r => r.SourceConcept)
                .Include(r => r.Document)
                .Where(r => r.TargetConceptId == current.Id)
                .ToListAsync(cancellationToken);

            foreach (var rel in incoming)
            {
                if (rel.SourceConcept != null && !visited.Contains(rel.SourceConcept.Id))
                {
                    visited.Add(rel.SourceConcept.Id);
                    frontier.Enqueue((rel.SourceConcept, level + 1));
                    result.Concepts.Add(rel.SourceConcept);
                }

                if (rel.DocumentId.HasValue)
                {
                    try
                    {
                        var doc = await _db.Documents.FindAsync(new object[] { rel.DocumentId.Value }, cancellationToken);
                        if (doc != null)
                        {
                            result.LinkedDocuments.Add(new GraphLinkedDocument
                            {
                                DocumentId = doc.Id,
                                Url = doc.Url,
                                Title = doc.Title,
                                Snippet = doc.Content?.Length > 500 ? doc.Content.Substring(0, 500) + "..." : doc.Content
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load document {DocumentId} referenced from relation", rel.DocumentId);
                    }
                }
            }
        }

        // Optionally: deduplicate linked documents
        result.LinkedDocuments = result.LinkedDocuments
            .GroupBy(d => d.DocumentId)
            .Select(g => g.First())
            .ToList();

        // Enrich results with vector-based documents for linked docs if available
        if (result.LinkedDocuments.Any())
        {
            var docIds = result.LinkedDocuments.Select(d => d.DocumentId).ToList();

            // Query top chunks for these documents
            var additionalChunks = new List<PostgresSearchResult>();
            foreach (var id in docIds)
            {
                var chunk = await _db.DocumentChunks
                    .Include(dc => dc.Document)
                    .Where(dc => dc.DocumentId == id && dc.Embedding != null)
                    .OrderByDescending(dc => dc.CreatedAt)
                    .Select(dc => new PostgresSearchResult
                    {
                        ChunkId = dc.Id,
                        DocumentId = dc.DocumentId,
                        Content = dc.Content,
                        SourceUrl = dc.Document.Url,
                        ChunkIndex = dc.ChunkIndex,
                        TokenCount = dc.TokenCount,
                        CreatedAt = dc.CreatedAt,
                        RelevanceScore = 0,
                        Metadata = new PostgresSearchMetadata
                        {
                            DocumentTitle = dc.Document.Title
                        }
                    })
                    .Take(3)
                    .ToListAsync(cancellationToken);

                additionalChunks.AddRange(chunk);
            }

            // Not directly used by caller now but kept for future extension
            result.LinkedChunks = additionalChunks;
        }

        return result;
    }
}

public class GraphRetrievalResult
{
    public List<Concept> Concepts { get; set; } = new();
    public List<GraphLinkedDocument> LinkedDocuments { get; set; } = new();
    public List<PostgresSearchResult> LinkedChunks { get; set; } = new();
}

public class GraphLinkedDocument
{
    public Guid DocumentId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Snippet { get; set; }
}
