namespace RagAgentApi.Services;

/// <summary>
/// Interface for OpenAI-compatible LLM services (Groq, Together, Fireworks, OpenRouter, etc.)
/// </summary>
public interface IOpenAICompatibleLlmService
{
    Task<string> GetChatCompletionAsync(string userPrompt, string context = "", CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> GetChatCompletionStreamAsync(string userPrompt, string context = "", CancellationToken cancellationToken = default);
}
