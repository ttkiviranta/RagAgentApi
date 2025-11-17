using System.ComponentModel.DataAnnotations;

namespace RagAgentApi.Models;

/// <summary>
/// Request model for querying the RAG system
/// </summary>
public class QueryRequest
{
    /// <summary>
    /// Conversation ID for context
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Query text to search for
    /// </summary>
    [Required]
    [MinLength(1)]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Number of top results to retrieve (1-50)
    /// </summary>
    [Range(1, 50)]
    public int TopK { get; set; } = 5;
}