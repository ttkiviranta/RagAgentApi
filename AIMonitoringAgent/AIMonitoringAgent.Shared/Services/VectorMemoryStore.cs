using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using AIMonitoringAgent.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AIMonitoringAgent.Shared.Services;

public interface IVectorMemoryStore
{
    Task InitializeAsync();
    Task StoreErrorAsync(VectorMemoryRecord record, float[] embedding);
    Task<List<VectorMemoryRecord>> SearchSimilarErrorsAsync(float[] embedding, int topK = 5);
    Task<VectorMemoryRecord?> GetErrorByFingerprintAsync(string fingerprintHash);
    Task UpdateErrorAsync(VectorMemoryRecord record);
    Task<List<VectorMemoryRecord>> GetRecentErrorsAsync(int days = 7, int limit = 100);
}

public class VectorMemoryStore : IVectorMemoryStore
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly ILogger<VectorMemoryStore> _logger;
    private readonly string _indexName = "ai-monitoring-errors";

    public VectorMemoryStore(
        SearchIndexClient indexClient,
        SearchClient searchClient,
        ILogger<VectorMemoryStore> logger)
    {
        _indexClient = indexClient;
        _searchClient = searchClient;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Check if index exists
            var indexNames = new List<string>();
            await foreach (var indexName in _indexClient.GetIndexNamesAsync())
            {
                indexNames.Add(indexName);
            }

            if (indexNames.Contains(_indexName))
            {
                _logger.LogInformation("Index {IndexName} already exists", _indexName);
                return;
            }

            // Create new index with fields
            var fields = new FieldBuilder().Build(typeof(VectorMemoryRecord));

            var definition = new SearchIndex(_indexName, fields);

            await _indexClient.CreateIndexAsync(definition);
            _logger.LogInformation("Created index {IndexName}", _indexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize vector store");
            throw;
        }
    }

    public async Task StoreErrorAsync(VectorMemoryRecord record, float[] embedding)
    {
        try
        {
            record.Id = record.FingerprintHash;
            record.EmbeddingVector = embedding.Select(x => (double)x).ToArray();
            record.Timestamp = DateTime.UtcNow;

            await _searchClient.UploadDocumentsAsync(new[] { record });

            _logger.LogInformation(
                "Stored error {FingerprintHash} to vector memory",
                record.FingerprintHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store error in vector memory");
            throw;
        }
    }

    public async Task<List<VectorMemoryRecord>> SearchSimilarErrorsAsync(float[] embedding, int topK = 5)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Size = topK,
                Select = { "*" }
            };

            var results = await _searchClient.SearchAsync<VectorMemoryRecord>(
                "*",
                searchOptions);

            var records = new List<VectorMemoryRecord>();

            // Iterate through search results
            foreach (var page in results.Value.GetResults())
            {
                records.Add(page.Document);
                if (records.Count >= topK) break;
            }

            _logger.LogInformation("Found {Count} similar errors", records.Count);
            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search similar errors");
            return new List<VectorMemoryRecord>();
        }
    }

    public async Task<VectorMemoryRecord?> GetErrorByFingerprintAsync(string fingerprintHash)
    {
        try
        {
            var result = await _searchClient.GetDocumentAsync<VectorMemoryRecord>(fingerprintHash);
            return result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get error by fingerprint");
            return null;
        }
    }

    public async Task UpdateErrorAsync(VectorMemoryRecord record)
    {
        try
        {
            record.Timestamp = DateTime.UtcNow;
            await _searchClient.MergeDocumentsAsync(new[] { record });

            _logger.LogInformation("Updated error {Id}", record.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update error");
            throw;
        }
    }

    public async Task<List<VectorMemoryRecord>> GetRecentErrorsAsync(int days = 7, int limit = 100)
    {
        try
        {
            var minDate = DateTime.UtcNow.AddDays(-days);
            var searchOptions = new SearchOptions
            {
                Filter = $"Timestamp ge {minDate:O}",
                Size = limit,
                OrderBy = { "Timestamp desc" },
                Select = { "*" }
            };

            var results = await _searchClient.SearchAsync<VectorMemoryRecord>(
                "*",
                searchOptions);

            var records = new List<VectorMemoryRecord>();

            // Iterate through search results
            foreach (var page in results.Value.GetResults())
            {
                records.Add(page.Document);
                if (records.Count >= limit) break;
            }

            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent errors");
            return new List<VectorMemoryRecord>();
        }
    }
}
