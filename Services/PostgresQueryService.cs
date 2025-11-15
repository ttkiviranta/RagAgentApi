using RagAgentApi.Data;
using RagAgentApi.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace RagAgentApi.Services;

/// <summary>
/// PostgreSQL-based query service for vector similarity search
/// </summary>
public class PostgresQueryService
{
    private readonly RagDbContext _context;
    private readonly ILogger<PostgresQueryService> _logger;

  public PostgresQueryService(RagDbContext context, ILogger<PostgresQueryService> logger)
    {
        _context = context;
  _logger = logger;
    }

    /// <summary>
 /// Search for similar document chunks using vector similarity
    /// </summary>
    /// <param name="queryEmbedding">Query vector (1536 dimensions)</param>
    /// <param name="topK">Number of results to return</param>
    /// <param name="minScore">Minimum similarity score (0-1, where 1 is identical)</param>
    /// <returns>List of search results ordered by relevance</returns>
    public async Task<List<PostgresSearchResult>> SearchAsync(
        float[] queryEmbedding,
   int topK = 5,
        double minScore = 0.5,
        CancellationToken cancellationToken = default)
    {
        try
        {
     _logger.LogInformation("[PostgresQueryService] Searching for {TopK} similar chunks", topK);

    var queryVector = new Vector(queryEmbedding);

  // Vector similarity search using cosine distance
     var results = await _context.DocumentChunks
        .Include(dc => dc.Document)
    .Where(dc => dc.Embedding != null && dc.Document.Status == "active")
         .Select(dc => new PostgresSearchResult
     {
     ChunkId = dc.Id,
      DocumentId = dc.DocumentId,
         Content = dc.Content,
       SourceUrl = dc.Document.Url,
     ChunkIndex = dc.ChunkIndex,
           TokenCount = dc.TokenCount,
    CreatedAt = dc.CreatedAt,
     // Calculate cosine similarity (1 - cosine distance)
   RelevanceScore = 1.0 - dc.Embedding!.CosineDistance(queryVector),
       Metadata = new PostgresSearchMetadata
      {
    DocumentTitle = dc.Document.Title,
   DocumentContentHash = dc.Document.ContentHash,
      DocumentScrapedAt = dc.Document.ScrapedAt,
     ChunkMetadata = dc.ChunkMetadata
      }
      })
        .Where(r => r.RelevanceScore >= minScore)
           .OrderByDescending(r => r.RelevanceScore)
      .Take(topK)
   .ToListAsync(cancellationToken);

          _logger.LogInformation("[PostgresQueryService] Found {Count} results with score >= {MinScore}", 
  results.Count, minScore);

     // Debug logging for top results
   foreach (var result in results.Take(3))
   {
 _logger.LogDebug("[PostgresQueryService] Result: Score={Score:F3}, URL='{URL}', Content='{Content}'",
   result.RelevanceScore,
     result.SourceUrl,
result.Content.Length > 100 ? result.Content.Substring(0, 100) + "..." : result.Content);
 }

            return results;
     }
   catch (Exception ex)
      {
    _logger.LogError(ex, "[PostgresQueryService] Vector search failed");
    throw;
   }
    }

  /// <summary>
    /// Search for similar past queries using message embeddings
    /// </summary>
  public async Task<List<SimilarQueryResult>> FindSimilarQueriesAsync(
    float[] queryEmbedding,
        int topK = 10,
   double minScore = 0.7,
        CancellationToken cancellationToken = default)
    {
   try
        {
            var queryVector = new Vector(queryEmbedding);

       var results = await _context.Messages
           .Include(m => m.Conversation)
  .Where(m => m.QueryEmbedding != null && m.Role == "user")
         .Select(m => new SimilarQueryResult
         {
  MessageId = m.Id,
  ConversationId = m.ConversationId,
  Query = m.Content,
      Answer = m.Conversation.Messages
       .Where(msg => msg.ConversationId == m.ConversationId && 
 msg.CreatedAt > m.CreatedAt && 
  msg.Role == "assistant")
        .OrderBy(msg => msg.CreatedAt)
        .Select(msg => msg.Content)
        .FirstOrDefault(),
     CreatedAt = m.CreatedAt,
   SimilarityScore = 1.0 - m.QueryEmbedding!.CosineDistance(queryVector)
        })
   .Where(r => r.SimilarityScore >= minScore)
       .OrderByDescending(r => r.SimilarityScore)
   .Take(topK)
     .ToListAsync(cancellationToken);

     _logger.LogInformation("[PostgresQueryService] Found {Count} similar queries", results.Count);
     return results;
  }
     catch (Exception ex)
     {
          _logger.LogError(ex, "[PostgresQueryService] Similar query search failed");
            throw;
 }
    }

    /// <summary>
    /// Get document statistics
    /// </summary>
    public async Task<PostgresSearchStats> GetSearchStatsAsync(CancellationToken cancellationToken = default)
    {
        return new PostgresSearchStats
      {
            TotalDocuments = await _context.Documents.CountAsync(cancellationToken),
      TotalChunks = await _context.DocumentChunks.CountAsync(cancellationToken),
     ActiveDocuments = await _context.Documents.CountAsync(d => d.Status == "active", cancellationToken),
  ChunksWithEmbeddings = await _context.DocumentChunks.CountAsync(dc => dc.Embedding != null, cancellationToken),
     TotalConversations = await _context.Conversations.CountAsync(cancellationToken),
 TotalMessages = await _context.Messages.CountAsync(cancellationToken)
        };
    }
}

/// <summary>
/// PostgreSQL search result
/// </summary>
public class PostgresSearchResult
{
   public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
 public int ChunkIndex { get; set; }
  public int? TokenCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public double RelevanceScore { get; set; }
    public PostgresSearchMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Search result metadata
/// </summary>
public class PostgresSearchMetadata
{
    public string? DocumentTitle { get; set; }
    public string DocumentContentHash { get; set; } = string.Empty;
    public DateTime DocumentScrapedAt { get; set; }
    public System.Text.Json.JsonDocument? ChunkMetadata { get; set; }
}

/// <summary>
/// Similar query result
/// </summary>
public class SimilarQueryResult
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
  public string Query { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public DateTime CreatedAt { get; set; }
    public double SimilarityScore { get; set; }
}

/// <summary>
/// Search statistics
/// </summary>
public class PostgresSearchStats
{
    public int TotalDocuments { get; set; }
    public int TotalChunks { get; set; }
    public int ActiveDocuments { get; set; }
    public int ChunksWithEmbeddings { get; set; }
   public int TotalConversations { get; set; }
    public int TotalMessages { get; set; }
}