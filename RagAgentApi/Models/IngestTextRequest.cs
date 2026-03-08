using System.ComponentModel.DataAnnotations;

namespace RagAgentApi.Models;

/// <summary>
/// Request model for ingesting raw text content (e.g., from file uploads)
/// </summary>
public class IngestTextRequest
{
    /// <summary>
    /// Raw text content to ingest
    /// </summary>
    [Required]
    [MinLength(10)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Title or name of the document
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Source of the content (e.g., "file-upload", "pdf", "user-input")
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Size of text chunks (100-5000 characters)
    /// </summary>
    [Range(100, 5000)]
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between chunks (must be less than ChunkSize/2)
    /// </summary>
    [Range(0, 2500)]
    public int ChunkOverlap { get; set; } = 200;
}
