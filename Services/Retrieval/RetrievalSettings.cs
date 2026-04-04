namespace RagAgentApi.Services.Retrieval;

/// <summary>
/// Configuration settings for retrieval strategies
/// </summary>
public class RetrievalSettings
{
    /// <summary>
    /// Retrieval mode: Rag, FileFirst, or Auto
    /// </summary>
    public string Mode { get; set; } = "Rag";

    /// <summary>
    /// Threshold for Auto mode: maximum number of documents for FileFirst
    /// </summary>
    public int AutoModeDocumentThreshold { get; set; } = 10;

    /// <summary>
    /// Threshold for Auto mode: maximum total content size (in KB) for FileFirst
    /// </summary>
    public int AutoModeContentSizeThresholdKb { get; set; } = 500;

    /// <summary>
    /// Minimum relevance score for search results
    /// </summary>
    public double MinimumRelevanceScore { get; set; } = 0.5;
}
