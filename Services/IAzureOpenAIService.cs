namespace RagAgentApi.Services;

/// <summary>
/// Service for interacting with Azure OpenAI
/// </summary>
public interface IAzureOpenAIService
{
    /// <summary>
    /// Get embedding for a single text
    /// </summary>
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get embeddings for multiple texts in batches
    /// </summary>
    Task<List<float[]>> GetEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get chat completion response
    /// </summary>
    Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}