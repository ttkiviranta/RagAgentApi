using RagAgentApi.Models;
using Azure.Search.Documents.Models;

namespace RagAgentApi.Services;

/// <summary>
/// Service for interacting with Azure AI Search
/// </summary>
public interface IAzureSearchService
{
    /// <summary>
    /// Create or update the search index
    /// </summary>
    Task CreateOrUpdateIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload documents to the search index
    /// </summary>
    Task UploadDocumentsAsync(List<Models.SearchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for documents using vector similarity
    /// </summary>
    Task<List<Azure.Search.Documents.Models.SearchResult<Models.SearchDocument>>> SearchAsync(float[] queryVector, int topK, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete documents by thread ID
    /// </summary>
    Task DeleteDocumentsByThreadIdAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the service is healthy
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete the search index
    /// </summary>
    Task DeleteIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recreate the search index (delete + create)
    /// </summary>
    Task RecreateIndexAsync(CancellationToken cancellationToken = default);
}