using System.ComponentModel.DataAnnotations;

namespace RagAgentApi.Models;

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

/// <summary>
/// Request model for file upload settings (query parameters)
/// </summary>
public class FileUploadSettings
{
    /// <summary>
    /// Size of text chunks (100-5000 characters)
    /// </summary>
    [Range(100, 5000)]
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between chunks
    /// </summary>
    [Range(0, 2500)]
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>
    /// Whether to store the original file in Blob Storage
    /// </summary>
    public bool StoreOriginalFile { get; set; } = true;

    /// <summary>
    /// Custom title for the document (optional, defaults to filename)
    /// </summary>
    public string? Title { get; set; }
}
