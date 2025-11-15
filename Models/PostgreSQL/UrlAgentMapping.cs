using System.ComponentModel.DataAnnotations;

namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// URL to Agent mapping entity for selecting specialized agents
/// </summary>
public class UrlAgentMapping
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
  [MaxLength(500)]
  public string Pattern { get; set; } = string.Empty; // Regex pattern

    [Required]
    public Guid AgentTypeId { get; set; }

    public int Priority { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual AgentType AgentType { get; set; } = null!;
}