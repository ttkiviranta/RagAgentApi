using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// Concept node for GraphRAG knowledge graph
/// </summary>
public class Concept
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument? Metadata { get; set; }

    // Navigation properties
    public virtual ICollection<Relation> OutgoingRelations { get; set; } = new List<Relation>();
    public virtual ICollection<Relation> IncomingRelations { get; set; } = new List<Relation>();
}
