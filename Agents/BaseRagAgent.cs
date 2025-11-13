using RagAgentApi.Models;

namespace RagAgentApi.Agents;

/// <summary>
/// Base class for all RAG agents in the system
/// </summary>
public abstract class BaseRagAgent
{
    protected readonly ILogger _logger;

    protected BaseRagAgent(ILogger logger)
    {
        _logger = logger;
  }

    /// <summary>
    /// Name of the agent
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Execute the agent's primary function
    /// </summary>
 public abstract Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a message to the context
    /// </summary>
    protected void AddMessage(AgentContext context, string to, string content, Dictionary<string, object>? data = null)
  {
     var message = new AgentMessage
        {
      From = Name,
   To = to,
       Content = content,
       Data = data
 };

        context.Messages.Add(message);
        context.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
  /// Log agent execution start
  /// </summary>
    protected void LogExecutionStart(string threadId)
    {
_logger.LogInformation("[{AgentName}] Starting execution for thread {ThreadId}", Name, threadId);
    }

    /// <summary>
    /// Log agent execution completion
 /// </summary>
  protected void LogExecutionComplete(string threadId, bool success, TimeSpan duration)
    {
  _logger.LogInformation("[{AgentName}] Completed execution for thread {ThreadId} in {Duration}ms - Success: {Success}", 
    Name, threadId, duration.TotalMilliseconds, success);
    }

  /// <summary>
    /// Handle and log exceptions
 /// </summary>
    protected AgentResult HandleException(Exception ex, string threadId, string operation)
  {
 _logger.LogError(ex, "[{AgentName}] Failed during {Operation} for thread {ThreadId}", 
   Name, operation, threadId);

return AgentResult.CreateFailure(
    $"{Name} failed during {operation}: {ex.Message}",
   new List<string> { ex.ToString() });
    }
}