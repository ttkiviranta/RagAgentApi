using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// Agent execution entity for debugging and analytics
/// </summary>
public class AgentExecution
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ThreadId { get; set; }

    [Required]
    [MaxLength(100)]
    public string AgentName { get; set; } = string.Empty;

    public Guid? ParentExecutionId { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument? InputData { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument? OutputData { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public int? DurationMs { get; set; }

    [Required]
  [MaxLength(20)]
    public string Status { get; set; } = "running"; // "running", "success", "failed"

    public string? ErrorMessage { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument? Metrics { get; set; }

    // Navigation properties (self-reference)
    public virtual AgentExecution? ParentExecution { get; set; }
    public virtual ICollection<AgentExecution> ChildExecutions { get; set; } = new List<AgentExecution>();
}