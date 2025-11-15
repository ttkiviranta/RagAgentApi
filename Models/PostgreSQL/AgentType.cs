using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// Agent type entity for specialized agents
/// </summary>
public class AgentType
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

  [MaxLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument? Capabilities { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument? ScraperConfig { get; set; }

 [Column(TypeName = "jsonb")]
    public JsonDocument? ChunkerConfig { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public JsonDocument AgentPipeline { get; set; } = JsonDocument.Parse("[]");

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<UrlAgentMapping> UrlMappings { get; set; } = new List<UrlAgentMapping>();
}