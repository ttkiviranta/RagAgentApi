using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Pgvector;

namespace RagAgentApi.Models.PostgreSQL;

/// <summary>
/// Message entity for conversation messages
/// </summary>
public class Message
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ConversationId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty; // "user", "assistant", "system"

    [Required]
    public string Content { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public JsonDocument? Sources { get; set; }

    [Column(TypeName = "vector(1536)")]
    public Vector? QueryEmbedding { get; set; }

    public int? TokenCount { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "jsonb")]
    public JsonDocument? Metadata { get; set; }

    // Navigation properties
    public virtual Conversation Conversation { get; set; } = null!;
}