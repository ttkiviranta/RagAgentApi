namespace RagAgentApi.Services;

/// <summary>
/// Unified LLM service that routes to the configured provider (Azure OpenAI or OpenAI-Compatible)
/// This service provides a single interface for chat completions regardless of backend
/// </summary>
public class LlmService
{
    private readonly IAzureOpenAIService _azureOpenAI;
    private readonly IOpenAICompatibleLlmService _openAICompatible;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmService> _logger;

    public LlmService(
        IAzureOpenAIService azureOpenAI,
        IOpenAICompatibleLlmService openAICompatible,
        IConfiguration configuration,
        ILogger<LlmService> logger)
    {
        _azureOpenAI = azureOpenAI;
        _openAICompatible = openAICompatible;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get chat completion from the configured LLM provider
    /// </summary>
    public async Task<string> GetChatCompletionAsync(string userPrompt, string context = "", CancellationToken cancellationToken = default)
    {
        var provider = GetActiveProvider();

        if (provider == LlmProviderType.OpenAICompatible)
        {
            _logger.LogDebug("[LlmService] Using OpenAI-compatible provider for chat completion");
            return await _openAICompatible.GetChatCompletionAsync(userPrompt, context, cancellationToken);
        }

        _logger.LogDebug("[LlmService] Using Azure OpenAI provider for chat completion");
        return await _azureOpenAI.GetChatCompletionAsync(userPrompt, context, cancellationToken);
    }

    /// <summary>
    /// Get streamed chat completion from the configured LLM provider
    /// </summary>
    public IAsyncEnumerable<string> GetChatCompletionStreamAsync(string userPrompt, string context = "", CancellationToken cancellationToken = default)
    {
        var provider = GetActiveProvider();

        if (provider == LlmProviderType.OpenAICompatible)
        {
            _logger.LogDebug("[LlmService] Using OpenAI-compatible provider for streamed chat completion");
            return _openAICompatible.GetChatCompletionStreamAsync(userPrompt, context, cancellationToken);
        }

        _logger.LogDebug("[LlmService] Using Azure OpenAI provider for streamed chat completion");
        return _azureOpenAI.GetChatCompletionStreamAsync(userPrompt, context, cancellationToken);
    }

    /// <summary>
    /// Determine which LLM provider is currently active based on configuration
    /// </summary>
    private LlmProviderType GetActiveProvider()
    {
        var providerName = _configuration.GetValue<string>("LlmProviders:Default", "AzureOpenAI");

        if (providerName.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase))
        {
            return LlmProviderType.OpenAICompatible;
        }

        return LlmProviderType.AzureOpenAI;
    }
}
