using System.Text.Json.Serialization;

namespace AIMonitoringAgent.Shared.Models;

public class AppInsightsException
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("operationName")]
    public string OperationName { get; set; } = string.Empty;

    [JsonPropertyName("exceptionType")]
    public string ExceptionType { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("stackTrace")]
    public string StackTrace { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("customDimensions")]
    public Dictionary<string, object> CustomDimensions { get; set; } = new();

    [JsonPropertyName("customProperties")]
    public Dictionary<string, string> CustomProperties { get; set; } = new();

    [JsonPropertyName("dependencyInfo")]
    public DependencyInfo? DependencyInfo { get; set; }
}

public class DependencyInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }
}

public class ErrorFingerprint
{
    public string FingerprintHash { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTracePattern { get; set; } = string.Empty;
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public int OccurrenceCount { get; set; }
    public List<string> AffectedOperations { get; set; } = new();
}

public class AnalysisResult
{
    [JsonPropertyName("errorId")]
    public string ErrorId { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("rootCauseAnalysis")]
    public string RootCauseAnalysis { get; set; } = string.Empty;

    [JsonPropertyName("isRecurring")]
    public bool IsRecurring { get; set; }

    [JsonPropertyName("similarErrorCount")]
    public int SimilarErrorCount { get; set; }

    [JsonPropertyName("deploymentCorrelation")]
    public DeploymentCorrelation? DeploymentCorrelation { get; set; }

    [JsonPropertyName("recommendedActions")]
    public List<string> RecommendedActions { get; set; } = new();

    [JsonPropertyName("affectedUsers")]
    public int AffectedUsers { get; set; }

    [JsonPropertyName("affectedOperations")]
    public List<string> AffectedOperations { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

public class DeploymentCorrelation
{
    public string DeploymentId { get; set; } = string.Empty;
    public DateTime DeploymentTime { get; set; }
    public string ReleaseName { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<string> ChangedFiles { get; set; } = new();
    public double TimeToErrorMinutes { get; set; }
    public bool IsLikeCause { get; set; }
}

public class VectorMemoryRecord
{
    public string Id { get; set; } = string.Empty;
    public string FingerprintHash { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTracePattern { get; set; } = string.Empty;
    public string RootCauseAnalysis { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int OccurrenceCount { get; set; }
    public List<string> AffectedOperations { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public string? DeploymentId { get; set; }
    public double[]? EmbeddingVector { get; set; }
}

public class NotificationConfig
{
    public bool Enabled { get; set; }
    public string Channel { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = new();
    public string? WebhookUrl { get; set; }
}

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? RelatedErrorId { get; set; }
}
