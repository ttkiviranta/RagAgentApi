namespace RagAgentApi.Services.DemoServices;

/// <summary>
/// Interface for demo services that generate test data and run demonstrations
/// </summary>
public interface IDemoService
{
    /// <summary>
    /// Generates test data required for the demo
    /// </summary>
    Task GenerateTestDataAsync();

    /// <summary>
    /// Runs the demo and returns results
    /// </summary>
    Task<DemoResult> RunDemoAsync();
}

/// <summary>
/// Result from running a demo
/// </summary>
public class DemoResult
{
    public string DemoType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public string ExecutionTimeMs { get; set; } = string.Empty;
}
