using System.ComponentModel.DataAnnotations;

namespace RagAgentApi.Models;

/// <summary>
/// Request model for ingesting content from a URL into the RAG system
/// </summary>
public class RagRequest
{
    /// <summary>
    /// URL to scrape and ingest
    /// </summary>
    [Required]
    [Url]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Size of text chunks (100-5000 characters)
    /// </summary>
 [Range(100, 5000)]
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Overlap between chunks (must be less than ChunkSize/2)
    /// </summary>
    [Range(0, 2500)]
    public int ChunkOverlap { get; set; } = 200;
}