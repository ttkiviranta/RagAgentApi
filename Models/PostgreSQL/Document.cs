using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// Document entity representing URL-level metadata
/// </summary>
public class Document
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string UrlHash { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Title { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string ContentHash { get; set; } = string.Empty;

    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "active";

    [Column(TypeName = "jsonb")]
    public JsonDocument? Metadata { get; set; }

    public int? ScrapingDurationMs { get; set; }

    // ─────────────────────────────────────────────────────────────────────
    // Azure Blob Storage metadata for original file storage
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full URI to the original file in Azure Blob Storage
    /// </summary>
    [MaxLength(2048)]
    public string? BlobUri { get; set; }

    /// <summary>
    /// Blob container name where the file is stored
    /// </summary>
    [MaxLength(256)]
    public string? BlobContainer { get; set; }

    /// <summary>
    /// Blob name (path within container)
    /// </summary>
    [MaxLength(1024)]
    public string? BlobName { get; set; }

    /// <summary>
    /// Original file name as uploaded by user
    /// </summary>
    [MaxLength(500)]
    public string? OriginalFileName { get; set; }

    /// <summary>
    /// MIME type of the original file (e.g., "application/pdf")
    /// </summary>
    [MaxLength(100)]
    public string? MimeType { get; set; }

    /// <summary>
    /// Size of the original file in bytes
    /// </summary>
    public long? OriginalFileSizeBytes { get; set; }

    /// <summary>
    /// SHA256 hash of the original file for integrity verification
    /// </summary>
    [MaxLength(64)]
    public string? OriginalFileHash { get; set; }

    /// <summary>
    /// Timestamp when the original file was uploaded to Blob Storage
    /// </summary>
    public DateTime? BlobUploadedAt { get; set; }

    /// <summary>
    /// Indicates if the original file is available in Blob Storage
    /// </summary>
    public bool HasOriginalFile => !string.IsNullOrEmpty(BlobUri);

    // Navigation properties
    public virtual ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}