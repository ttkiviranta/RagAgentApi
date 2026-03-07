namespace RagAgentApi.Models;

/// <summary>
/// Represents an error log entry stored in the database
/// </summary>
public class ErrorLog
{
    /// <summary>
    /// Unique identifier for the error log entry
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique error ID from analysis
    /// </summary>
    public string ErrorId { get; set; } = string.Empty;

    /// <summary>
    /// Exception type that occurred
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// Exception message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Stack trace of the exception
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Error severity level (INFO, WARNING, ERROR, CRITICAL)
    /// </summary>
    public string Severity { get; set; } = "ERROR";

    /// <summary>
    /// Error category for classification
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Root cause analysis
    /// </summary>
    public string RootCauseAnalysis { get; set; } = string.Empty;

    /// <summary>
    /// Recommended actions (JSON array stored as string)
    /// </summary>
    public string RecommendedActions { get; set; } = "[]";

    /// <summary>
    /// Affected operations (JSON array stored as string)
    /// </summary>
    public string AffectedOperations { get; set; } = "[]";

    /// <summary>
    /// Operation name that caused the error
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Request ID for tracing
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Is this a recurring error
    /// </summary>
    public bool IsRecurring { get; set; } = false;

    /// <summary>
    /// Number of similar errors
    /// </summary>
    public int SimilarErrorCount { get; set; } = 0;

    /// <summary>
    /// Number of affected users
    /// </summary>
    public int AffectedUsers { get; set; } = 0;

    /// <summary>
    /// When the error occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether notification was sent
    /// </summary>
    public bool NotificationSent { get; set; } = false;

    /// <summary>
    /// When notification was sent
    /// </summary>
    public DateTime? NotificationSentAt { get; set; }

    /// <summary>
    /// Notification channels used (JSON array)
    /// </summary>
    public string NotificationChannels { get; set; } = "[]";

    /// <summary>
    /// Additional metadata (JSON)
    /// </summary>
    public string? Metadata { get; set; }
}
