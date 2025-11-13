namespace RagAgentApi.Models;

/// <summary>
/// Represents the result of an agent execution
/// </summary>
public class AgentResult
{
    /// <summary>
    /// Indicates if the agent execution was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result message or description
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Result data payload
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

  /// <summary>
    /// List of errors if execution failed
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Creates a successful result
    /// </summary>
  public static AgentResult CreateSuccess(string message, Dictionary<string, object>? data = null)
    {
        return new AgentResult
    {
            Success = true,
      Message = message,
    Data = data ?? new()
        };
    }

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static AgentResult CreateFailure(string message, List<string>? errors = null)
    {
      return new AgentResult
        {
 Success = false,
   Message = message,
       Errors = errors ?? new()
 };
    }
}