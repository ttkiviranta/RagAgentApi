using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Pgvector;

namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// Document chunk entity with vector embeddings
/// </summary>
public class DocumentChunk
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    [Required]
  public string Content { get; set; } = string.Empty;

    public int? TokenCount { get; set; }

    [Column(TypeName = "vector(1536)")]
    public Vector? Embedding { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument? ChunkMetadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Document Document { get; set; } = null!;
}