namespace RagAgentApi.Services.Retrieval;

/// <summary>
/// Result from a retrieval strategy execution
/// </summary>
public class RetrievalResult
{
    /// <summary>
    /// The generated answer
    /// </summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// List of source documents used
    /// </summary>
    public List<RetrievalSource> Sources { get; set; } = new();

    /// <summary>
    /// The strategy that was used (Rag or FileFirst)
    /// </summary>
    public string StrategyUsed { get; set; } = string.Empty;

    /// <summary>
    /// The configured mode (Rag, FileFirst, or Auto)
    /// </summary>
    public string ConfiguredMode { get; set; } = string.Empty;

    /// <summary>
    /// Whether the query was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if unsuccessful
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Additional metadata about the retrieval
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Source document information
/// </summary>
public class RetrievalSource
{
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public string? Title { get; set; }

    /// <summary>
    /// Additional metadata about the source (e.g., blob info, file size)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
