namespace RagAgentApi.Services.DemoServices;

/// <summary>
/// Interface for flexible test data sourcing (local files or database)
/// </summary>
public interface ITestDataRepository
{
    /// <summary>
    /// Gets test data for specified demo type
    /// </summary>
    Task<string> GetTestDataAsync(string demoType);

    /// <summary>
    /// Saves demo execution result
    /// </summary>
    Task SaveDemoResultAsync(string demoType, DemoResult result);

    /// <summary>
    /// Retrieves recent demo results
    /// </summary>
    Task<List<DemoResult>> GetDemoResultsAsync(string demoType, int count = 10);
}
