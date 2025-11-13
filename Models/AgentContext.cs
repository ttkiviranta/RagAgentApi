namespace RagAgentApi.Models;

/// <summary>
/// Represents the execution context for an agent thread
/// </summary>
public class AgentContext
{
    /// <summary>
    /// Unique identifier for this thread
    /// </summary>
    public string ThreadId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Shared state between agents in this thread
    /// </summary>
    public Dictionary<string, object> State { get; set; } = new();

    /// <summary>
    /// Messages exchanged between agents
 /// </summary>
    public List<AgentMessage> Messages { get; set; } = new();

  /// <summary>
    /// Timestamp when the context was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when the context was last updated
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}