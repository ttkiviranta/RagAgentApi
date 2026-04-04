namespace RagAgentApi.Services.Retrieval;

/// <summary>
/// Interface for retrieval strategies.
/// Implementations determine how documents are retrieved and used to answer queries.
/// </summary>
public interface IRetrievalStrategy
{
    /// <summary>
    /// Gets the name of this strategy
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the retrieval strategy to answer a query
    /// </summary>
    /// <param name="query">The user's question</param>
    /// <param name="topK">Maximum number of documents to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The retrieval result containing answer and sources</returns>
    Task<RetrievalResult> ExecuteAsync(string query, int topK, CancellationToken cancellationToken = default);
}
