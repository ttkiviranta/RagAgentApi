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

    // Navigation properties
    public virtual ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}