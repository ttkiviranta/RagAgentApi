using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using RagAgentApi.Models;

namespace RagAgentApi.Services;

public class AzureSearchService : IAzureSearchService
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly AzureSearchConfig _config;
private readonly ILogger<AzureSearchService> _logger;

    public AzureSearchService(IConfiguration configuration, ILogger<AzureSearchService> logger)
    {
        _logger = logger;
        _config = new AzureSearchConfig();
        configuration.GetSection("Azure:Search").Bind(_config);

  _indexClient = new SearchIndexClient(
            new Uri(_config.Endpoint),
        new AzureKeyCredential(_config.Key));

        _searchClient = new SearchClient(
            new Uri(_config.Endpoint),
       _config.IndexName,
            new AzureKeyCredential(_config.Key));
    }

    public async Task CreateOrUpdateIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var index = new SearchIndex(_config.IndexName)
    {
   Fields =
     {
        new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
        new SearchableField("content"),
     new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
  {
       VectorSearchDimensions = 1536,
   VectorSearchProfileName = "vector-profile"
            },
         new SimpleField("sourceUrl", SearchFieldDataType.String) { IsFilterable = true },
       new SimpleField("threadId", SearchFieldDataType.String) { IsFilterable = true },
         new SimpleField("chunkIndex", SearchFieldDataType.Int32) { IsFilterable = true },
      new SimpleField("createdAt", SearchFieldDataType.DateTimeOffset) 
    { 
        IsFilterable = true, 
      IsSortable = true 
      }
           },
      VectorSearch = new VectorSearch
   {
             Profiles = 
   { 
 new VectorSearchProfile("vector-profile", "hnsw-config") 
           },
       Algorithms = 
           { 
       new HnswAlgorithmConfiguration("hnsw-config") 
      }
          }
          };

        await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
 _logger.LogInformation("Search index '{IndexName}' created or updated successfully", _config.IndexName);
        }
        catch (Exception ex)
      {
_logger.LogError(ex, "Failed to create or update search index '{IndexName}'", _config.IndexName);
            throw;
        }
    }

    public async Task UploadDocumentsAsync(List<Models.SearchDocument> documents, CancellationToken cancellationToken = default)
    {
        if (!documents.Any())
 {
          _logger.LogWarning("No documents to upload");
return;
}

        try
        {
          // Debug logging before upload
    foreach (var doc in documents.Take(3))
      {
      _logger.LogDebug("[AzureSearch] Uploading doc ID='{Id}', Content length={Length}, URL='{URL}'",
        doc.Id, doc.Content?.Length ?? 0, doc.SourceUrl);
      }

       var batch = IndexDocumentsBatch.Upload(documents);
    var response = await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);

  var successCount = response.Value.Results.Count(r => r.Succeeded);
            var failCount = response.Value.Results.Count(r => !r.Succeeded);

  _logger.LogInformation("Uploaded {SuccessCount} documents successfully, {FailCount} failed", 
       successCount, failCount);

       if (failCount > 0)
      {
     var failures = response.Value.Results
   .Where(r => !r.Succeeded)
      .Select(r => $"Key: {r.Key}, Error: {r.ErrorMessage}")
        .ToList();

   _logger.LogWarning("Document upload failures: {Failures}", string.Join("; ", failures));
     }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload {Count} documents to search index", documents.Count);
            throw;
        }
    }

  public async Task<List<Azure.Search.Documents.Models.SearchResult<Models.SearchDocument>>> SearchAsync(float[] queryVector, int topK, CancellationToken cancellationToken = default)
    {
        try
        {
 var searchOptions = new SearchOptions
     {
       Size = topK,
       Select = { "id", "content", "sourceUrl", "threadId", "chunkIndex", "createdAt" },  // ← Explicitly select fields
    VectorSearch = new()
        {
           Queries = 
       {
       new VectorizedQuery(queryVector)
                {
  KNearestNeighborsCount = topK,
 Fields = { "contentVector" }
      }
 }
  }
   };

    var response = await _searchClient.SearchAsync<Models.SearchDocument>("*", searchOptions, cancellationToken);
    var results = new List<Azure.Search.Documents.Models.SearchResult<Models.SearchDocument>>();

         await foreach (var result in response.Value.GetResultsAsync())
   {
  results.Add(result);
   }

         _logger.LogDebug("Vector search returned {Count} results", results.Count);
        
// Debug logging to see what we actually get
        foreach (var result in results.Take(3))
   {
        _logger.LogDebug("Search result: Score={Score}, Content='{Content}', URL='{URL}'", 
          result.Score,
   result.Document?.Content?.Substring(0, Math.Min(result.Document.Content?.Length ?? 0, 50)) ?? "null",
      result.Document?.SourceUrl ?? "null");
       }

      return results;
        }
  catch (Exception ex)
{
      _logger.LogError(ex, "Failed to perform vector search with top-K {TopK}", topK);
 throw;
        }
    }

    public async Task DeleteDocumentsByThreadIdAsync(string threadId, CancellationToken cancellationToken = default)
    {
        try
        {
            var searchOptions = new SearchOptions
     {
   Filter = $"threadId eq '{threadId}'",
            Select = { "id" }
 };

  var response = await _searchClient.SearchAsync<Models.SearchDocument>("*", searchOptions, cancellationToken);
            var documentsToDelete = new List<Models.SearchDocument>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
          documentsToDelete.Add(new Models.SearchDocument { Id = result.Document.Id });
       }

 if (documentsToDelete.Any())
    {
         var batch = IndexDocumentsBatch.Delete(documentsToDelete);
     await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);

   _logger.LogInformation("Deleted {Count} documents for thread {ThreadId}", 
       documentsToDelete.Count, threadId);
            }
        }
        catch (Exception ex)
        {
    _logger.LogError(ex, "Failed to delete documents for thread {ThreadId}", threadId);
            throw;
   }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
     try
    {
            // Try to get index statistics
   var index = await _indexClient.GetIndexAsync(_config.IndexName, cancellationToken);
            return index != null;
        }
        catch (Exception ex)
  {
       _logger.LogError(ex, "Health check failed for Azure Search service");
     return false;
}
    }

    public async Task DeleteIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
         await _indexClient.DeleteIndexAsync(_config.IndexName, cancellationToken);
         _logger.LogInformation("Deleted search index '{IndexName}'", _config.IndexName);
  }
     catch (RequestFailedException ex) when (ex.Status == 404)
        {
    _logger.LogInformation("Index '{IndexName}' doesn't exist, nothing to delete", _config.IndexName);
    }
  catch (Exception ex)
        {
     _logger.LogError(ex, "Failed to delete search index '{IndexName}'", _config.IndexName);
    throw;
        }
    }

    public async Task RecreateIndexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recreating search index '{IndexName}'...", _config.IndexName);
        
        // Delete existing index
        await DeleteIndexAsync(cancellationToken);
        
 // Wait a moment for deletion to complete
        await Task.Delay(2000, cancellationToken);
  
        // Create new index
        await CreateOrUpdateIndexAsync(cancellationToken);
      
  _logger.LogInformation("Search index '{IndexName}' recreated successfully", _config.IndexName);
    }

    private class AzureSearchConfig
    {
        public string Endpoint { get; set; } = string.Empty;
      public string Key { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
    }
}