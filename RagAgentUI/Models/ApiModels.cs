namespace RagAgentUI.Models;

// DTOs
public record IngestResponse(Guid ThreadId, string Message, string Status);
public record QueryResponse(string Answer, List<SourceDto> Sources);
public record SourceDto(string Url, string Content, double RelevanceScore);
public record ConversationDto(Guid Id, string Title, DateTime CreatedAt, DateTime LastMessageAt, int MessageCount);

public record CreateConversationResponse(Guid Id, string Title, DateTime CreatedAt);
public record MessageDto(Guid Id, string Role, string Content, DateTime CreatedAt, List<SourceDto>? Sources);
public record AgentStatsResponse(List<AgentStatDto> Stats);
public record AgentStatDto(string AgentName, int ExecutionCount, double AvgDurationMs, double SuccessRate);

/// <summary>
/// Result of checking if a document URL already exists
/// </summary>
public class DocumentUrlCheckResult
{
    public bool Exists { get; set; }
    public string Url { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
    public string? Title { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Response model for file upload with blob storage
/// </summary>
public class FileUploadResponse
{
    public Guid ThreadId { get; set; }
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = "success";
    public string Message { get; set; } = string.Empty;

    // File information
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? MimeType { get; set; }

    // Blob storage information
    public bool StoredInBlobStorage { get; set; }
    public string? BlobUri { get; set; }
    public string? FileHash { get; set; }

    // Processing information
    public int ChunksProcessed { get; set; }
    public int ExecutionTimeMs { get; set; }
    public string RetrievalMode { get; set; } = "Rag";
}