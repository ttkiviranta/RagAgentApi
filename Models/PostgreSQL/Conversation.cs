using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// Conversation entity for managing user conversations
/// </summary>
public class Conversation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)]
    public string? UserId { get; set; }

    [MaxLength(500)]
 public string? Title { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

 public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public int MessageCount { get; set; } = 0;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "active";

    [Column(TypeName = "jsonb")]
    public JsonDocument? Metadata { get; set; }

    // Navigation properties
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}