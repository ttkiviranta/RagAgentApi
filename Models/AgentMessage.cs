namespace RagAgentApi.Models;

/// <summary>
/// Represents a message between agents in the system
/// </summary>
public class AgentMessage
{
  /// <summary>
 /// Name of the agent sending the message
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Name of the agent receiving the message
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

  /// <summary>
    /// Additional data payload
  /// </summary>
    public Dictionary<string, object>? Data { get; set; }

 /// <summary>
 /// Timestamp when the message was created
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}