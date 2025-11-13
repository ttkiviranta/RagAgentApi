using Azure.Search.Documents.Indexes;
using System.Text.Json.Serialization;

namespace RagAgentApi.Models;

/// <summary>
/// Document model for Azure AI Search
/// </summary>
public class SearchDocument
{
    /// <summary>
    /// Unique identifier for the document
    /// </summary>
    [SimpleField(IsKey = true)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Text content of the document chunk
    /// </summary>
    [SearchableField]
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Vector embedding of the content
    /// </summary>
    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "vector-profile")]
    [JsonPropertyName("contentVector")]
    public IReadOnlyList<float> ContentVector { get; set; } = new List<float>();

    /// <summary>
    /// Source URL where the content was scraped from
    /// </summary>
    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Thread ID that processed this document
    /// </summary>
    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("threadId")]
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    /// Index of this chunk within the source document
    /// </summary>
    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("chunkIndex")]
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Timestamp when the document was created
    /// </summary>
    [SimpleField(IsFilterable = true, IsSortable = true)]
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}