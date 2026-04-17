namespace RagAgentApi.Models;

public class PdfProcessingMessage
{
    public Guid DocumentId { get; set; }
    public Guid ThreadId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public int ChunkSize { get; set; } = 1000;
    public int ChunkOverlap { get; set; } = 200;
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
}
