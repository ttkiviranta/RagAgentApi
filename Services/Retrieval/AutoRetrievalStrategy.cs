using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RagAgentApi.Data;

namespace RagAgentApi.Services.Retrieval;

/// <summary>
/// Auto retrieval strategy that automatically selects between Rag and FileFirst
/// based on document collection size, content, and blob availability.
/// Includes automatic fallback from FileFirst to Rag if blob storage fails.
/// </summary>
public class AutoRetrievalStrategy : IRetrievalStrategy
{
    private readonly RagDbContext _dbContext;
    private readonly RagRetrievalStrategy _ragStrategy;
    private readonly FileFirstRetrievalStrategy _fileFirstStrategy;
    private readonly IBlobStorageService? _blobStorageService;
    private readonly ILogger<AutoRetrievalStrategy> _logger;
    private readonly RetrievalSettings _settings;

    public string Name => "Auto";

    public AutoRetrievalStrategy(
        RagDbContext dbContext,
        RagRetrievalStrategy ragStrategy,
        FileFirstRetrievalStrategy fileFirstStrategy,
        IOptions<RetrievalSettings> settings,
        ILogger<AutoRetrievalStrategy> logger,
        IBlobStorageService? blobStorageService = null)
    {
        _dbContext = dbContext;
        _ragStrategy = ragStrategy;
        _fileFirstStrategy = fileFirstStrategy;
        _settings = settings.Value;
        _logger = logger;
        _blobStorageService = blobStorageService;
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
                "[AutoStrategy] Selected '{Strategy}' based on: {DocCount} documents, {TotalSizeKb}KB total, {BlobCount} with blobs",
                selectedStrategy.Name, stats.DocumentCount, stats.TotalContentSizeKb, stats.DocumentsWithBlobs);

            // Execute the selected strategy
            var result = await selectedStrategy.ExecuteAsync(query, topK, cancellationToken);

            // Check for FileFirst failure and fallback to RAG
            if (!result.Success && selectedStrategy.Name == "FileFirst")
            {
                _logger.LogWarning("[AutoStrategy] FileFirst failed, falling back to RAG strategy");
                result = await _ragStrategy.ExecuteAsync(query, topK, cancellationToken);
                result.Metadata["fallback_reason"] = "FileFirst strategy failed, used RAG fallback";
                result.Metadata["original_strategy"] = "FileFirst";
            }

            // Update result to reflect Auto mode
            result.ConfiguredMode = "Auto";
            result.Metadata["auto_selection_reason"] = GetSelectionReason(stats, selectedStrategy.Name);
            result.Metadata["document_count"] = stats.DocumentCount;
            result.Metadata["total_content_size_kb"] = stats.TotalContentSizeKb;
            result.Metadata["documents_with_blobs"] = stats.DocumentsWithBlobs;
            result.Metadata["blob_storage_available"] = _blobStorageService?.IsEnabled ?? false;

            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoStrategy] Error during retrieval, attempting RAG fallback");

            // Final fallback to RAG
            try
            {
                var fallbackResult = await _ragStrategy.ExecuteAsync(query, topK, cancellationToken);
                fallbackResult.ConfiguredMode = "Auto";
                fallbackResult.Metadata["fallback_reason"] = $"Error during auto-selection: {ex.Message}";
                stopwatch.Stop();
                fallbackResult.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return fallbackResult;
            }
            catch (Exception ragEx)
            {
                _logger.LogError(ragEx, "[AutoStrategy] RAG fallback also failed");
                stopwatch.Stop();
                return new RetrievalResult
                {
                    Success = false,
                    ErrorMessage = $"Both strategies failed. Original: {ex.Message}, Fallback: {ragEx.Message}",
                    StrategyUsed = Name,
                    ConfiguredMode = "Auto",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }
    }

    private IRetrievalStrategy SelectStrategy(DocumentStats stats)
    {
        // Prefer FileFirst if:
        // - Document count is small (below threshold)
        // - AND total content size is small (below threshold)
        // - AND we have documents with blob storage OR small total content
        bool isSmallCollection = stats.DocumentCount <= _settings.AutoModeDocumentThreshold 
            && stats.TotalContentSizeKb <= _settings.AutoModeContentSizeThresholdKb;

        bool hasBlobsAvailable = stats.DocumentsWithBlobs > 0 && _blobStorageService?.IsEnabled == true;

        if (isSmallCollection && (hasBlobsAvailable || stats.TotalContentSizeKb < 100))
        {
            _logger.LogInformation(
                "[AutoStrategy] Selecting FileFirst: {DocCount} docs <= {Threshold}, {SizeKb}KB <= {SizeThreshold}KB, {BlobCount} blobs",
                stats.DocumentCount, _settings.AutoModeDocumentThreshold,
                stats.TotalContentSizeKb, _settings.AutoModeContentSizeThresholdKb,
                stats.DocumentsWithBlobs);
            return _fileFirstStrategy;
        }

        // Otherwise use RAG for large collections
        _logger.LogInformation("[AutoStrategy] Selecting Rag: collection exceeds thresholds or no blobs available");
        return _ragStrategy;
    }

    private string GetSelectionReason(DocumentStats stats, string selectedStrategy)
    {
        if (selectedStrategy == "FileFirst")
        {
            if (stats.DocumentsWithBlobs > 0)
                return $"Small collection ({stats.DocumentCount} docs, {stats.DocumentsWithBlobs} with original files) - using FileFirst for complete context";
            return $"Small collection ({stats.DocumentCount} docs, {stats.TotalContentSizeKb}KB) - using FileFirst for complete context";
        }

        return $"Large collection ({stats.DocumentCount} docs, {stats.TotalContentSizeKb}KB) - using Rag for efficient semantic search";
    }

    private async Task<DocumentStats> GetDocumentStatsAsync(CancellationToken cancellationToken)
    {
        var stats = await _dbContext.Documents
            .Where(d => d.Status == "active" || d.Status == "completed")
            .GroupBy(d => 1)
            .Select(g => new DocumentStats
            {
                DocumentCount = g.Count(),
                TotalContentSizeKb = (int)(g.Sum(d => d.Content != null ? d.Content.Length : 0) / 1024),
                DocumentsWithBlobs = g.Count(d => d.BlobUri != null)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new DocumentStats();
    }

    private class DocumentStats
    {
        public int DocumentCount { get; set; }
        public int TotalContentSizeKb { get; set; }
        public int DocumentsWithBlobs { get; set; }
    }
}
