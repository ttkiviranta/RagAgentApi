using RagAgentApi.Models;
using RagAgentApi.Services;
using AIMonitoringAgent.Shared.Models;

namespace RagAgentApi.Agents;

/// <summary>
/// Base class for all RAG agents in the system
/// </summary>
public abstract class BaseRagAgent
{
    protected readonly ILogger _logger;
    protected readonly IErrorLogService? _errorLogService;

    protected BaseRagAgent(ILogger logger, IErrorLogService? errorLogService = null)
    {
        _logger = logger;
        _errorLogService = errorLogService;
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
    /// Handle and log exceptions - also saves to ErrorLog database
    /// </summary>
    protected AgentResult HandleException(Exception ex, string threadId, string operation)
    {
        _logger.LogError(ex, "[{AgentName}] Failed during {Operation} for thread {ThreadId}", 
            Name, operation, threadId);

        // Log to ErrorDashboard if service is available
        if (_errorLogService != null)
        {
            try
            {
                var analysis = new AnalysisResult
                {
                    ErrorId = $"agent-{Guid.NewGuid().ToString()[..8]}",
                    Severity = DetermineSeverity(ex),
                    Category = "Pipeline",
                    RootCauseAnalysis = $"[{Name}] Failed during {operation}: {ex.Message}",
                    RecommendedActions = new List<string>
                    {
                        $"Check {Name} configuration and dependencies",
                        "Review input data in AgentContext",
                        "Check external service connectivity"
                    },
                    AffectedOperations = new List<string> { Name, operation },
                    IsRecurring = false,
                    SimilarErrorCount = 0,
                    AffectedUsers = 1,
                    Timestamp = DateTime.UtcNow
                };

                var appException = new AppInsightsException
                {
                    ExceptionType = ex.GetType().Name,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace ?? "No stack trace",
                    Timestamp = DateTime.UtcNow,
                    OperationName = $"{Name}.{operation}",
                    RequestId = threadId
                };

                // Fire and forget - don't block pipeline execution
                _ = _errorLogService.LogErrorAsync(analysis, appException, Array.Empty<string>());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log agent error to database");
            }
        }

        return AgentResult.CreateFailure(
            $"{Name} failed during {operation}: {ex.Message}",
            new List<string> { ex.ToString() });
    }

    /// <summary>
    /// Determine error severity based on exception type
    /// </summary>
    private static string DetermineSeverity(Exception ex)
    {
        return ex switch
        {
            HttpRequestException => "ERROR",
            TimeoutException => "ERROR",
            OperationCanceledException => "WARNING",
            ArgumentException => "WARNING",
            InvalidOperationException => "ERROR",
            _ => "ERROR"
        };
    }
}