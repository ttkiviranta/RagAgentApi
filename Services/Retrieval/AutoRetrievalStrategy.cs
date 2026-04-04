using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RagAgentApi.Data;

namespace RagAgentApi.Services.Retrieval;

/// <summary>
/// Auto retrieval strategy that automatically selects between Rag and FileFirst
/// based on document collection size and content.
/// </summary>
public class AutoRetrievalStrategy : IRetrievalStrategy
{
    private readonly RagDbContext _dbContext;
    private readonly RagRetrievalStrategy _ragStrategy;
    private readonly FileFirstRetrievalStrategy _fileFirstStrategy;
    private readonly ILogger<AutoRetrievalStrategy> _logger;
    private readonly RetrievalSettings _settings;

    public string Name => "Auto";

    public AutoRetrievalStrategy(
        RagDbContext dbContext,
        RagRetrievalStrategy ragStrategy,
        FileFirstRetrievalStrategy fileFirstStrategy,
        IOptions<RetrievalSettings> settings,
        ILogger<AutoRetrievalStrategy> logger)
    {
        _dbContext = dbContext;
        _ragStrategy = ragStrategy;
        _fileFirstStrategy = fileFirstStrategy;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RetrievalResult> ExecuteAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Analyze document collection to decide which strategy to use
            var stats = await GetDocumentStatsAsync(cancellationToken);
            var selectedStrategy = SelectStrategy(stats);

            _logger.LogInformation(
                "[AutoStrategy] Selected '{Strategy}' based on: {DocCount} documents, {TotalSizeKb}KB total size",
                selectedStrategy.Name, stats.DocumentCount, stats.TotalContentSizeKb);

            // Execute the selected strategy
            var result = await selectedStrategy.ExecuteAsync(query, topK, cancellationToken);

            // Update result to reflect Auto mode
            result.ConfiguredMode = "Auto";
            result.Metadata["auto_selection_reason"] = stats.DocumentCount <= _settings.AutoModeDocumentThreshold 
                && stats.TotalContentSizeKb <= _settings.AutoModeContentSizeThresholdKb
                    ? "Small collection - using FileFirst for complete context"
                    : "Large collection - using Rag for efficient semantic search";
            result.Metadata["document_count"] = stats.DocumentCount;
            result.Metadata["total_content_size_kb"] = stats.TotalContentSizeKb;

            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoStrategy] Error during retrieval");
            stopwatch.Stop();
            return new RetrievalResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                StrategyUsed = Name,
                ConfiguredMode = "Auto",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private IRetrievalStrategy SelectStrategy(DocumentStats stats)
    {
        // Use FileFirst if:
        // - Document count is small (below threshold)
        // - AND total content size is small (below threshold)
        if (stats.DocumentCount <= _settings.AutoModeDocumentThreshold 
            && stats.TotalContentSizeKb <= _settings.AutoModeContentSizeThresholdKb)
        {
            _logger.LogInformation("[AutoStrategy] Selecting FileFirst: {DocCount} docs <= {Threshold}, {SizeKb}KB <= {SizeThreshold}KB",
                stats.DocumentCount, _settings.AutoModeDocumentThreshold,
                stats.TotalContentSizeKb, _settings.AutoModeContentSizeThresholdKb);
            return _fileFirstStrategy;
        }

        // Otherwise use RAG for large collections
        _logger.LogInformation("[AutoStrategy] Selecting Rag: collection exceeds thresholds");
        return _ragStrategy;
    }

    private async Task<DocumentStats> GetDocumentStatsAsync(CancellationToken cancellationToken)
    {
        var stats = await _dbContext.Documents
            .Where(d => d.Status == "active" || d.Status == "completed")
            .GroupBy(d => 1)
            .Select(g => new DocumentStats
            {
                DocumentCount = g.Count(),
                TotalContentSizeKb = (int)(g.Sum(d => d.Content != null ? d.Content.Length : 0) / 1024)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new DocumentStats();
    }

    private class DocumentStats
    {
        public int DocumentCount { get; set; }
        public int TotalContentSizeKb { get; set; }
    }
}
