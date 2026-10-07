using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// Directed relation between two concepts. Optionally links to a document.
/// </summary>
public class Relation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SourceConceptId { get; set; }
    public Concept? SourceConcept { get; set; }

    [Required]
    public Guid TargetConceptId { get; set; }
    public Concept? TargetConcept { get; set; }

    [MaxLength(128)]
    public string? RelationType { get; set; }

    public double Weight { get; set; } = 1.0;

    // Optional reference to a Document that this relation points to
    public Guid? DocumentId { get; set; }
    public Models.PostgreSQL.Document? Document { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument? Metadata { get; set; }
}
