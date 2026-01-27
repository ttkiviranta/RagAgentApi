namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// Demo execution result stored in PostgreSQL
/// </summary>
public class DemoExecution
{
    public Guid Id { get; set; }
    public string DemoType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ResultData { get; set; } = string.Empty; // JSON serialized
    public long ExecutionTimeMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Test data file metadata
/// </summary>
public class DemoTestData
{
    public Guid Id { get; set; }
    public string DemoType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty; // Local path or blob URI
    public long FileSizeBytes { get; set; }
    public string ContentHash { get; set; } = string.Empty; // For cache validation
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
