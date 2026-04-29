namespace RagAgentApi.Services;

/// <summary>
/// Factory for creating LLM service instances based on configuration
/// Allows switching between Azure OpenAI and OpenAI-compatible providers (Groq, Together, Fireworks, OpenRouter, etc.)
/// </summary>
public class LlmServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmServiceFactory> _logger;

    public LlmServiceFactory(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<LlmServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get the LLM service based on configuration
    /// </summary>
    /// <returns>IAzureOpenAIService or IOpenAICompatibleLlmService wrapper</returns>
    public object GetLlmService()
    {
        var providerType = _configuration.GetValue<string>("LlmProviders:Default", "AzureOpenAI");

        if (providerType.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[LlmServiceFactory] Using OpenAI-compatible LLM provider");
            return _serviceProvider.GetRequiredService<IOpenAICompatibleLlmService>();
        }

        _logger.LogInformation("[LlmServiceFactory] Using Azure OpenAI LLM provider");
        return _serviceProvider.GetRequiredService<IAzureOpenAIService>();
    }

    /// <summary>
    /// Get Azure OpenAI service directly (for embeddings and fallback)
    /// </summary>
    public IAzureOpenAIService GetAzureOpenAIService()
    {
        return _serviceProvider.GetRequiredService<IAzureOpenAIService>();
    }

    /// <summary>
    /// Get OpenAI-compatible service directly (for Qwen and other compatible models)
    /// </summary>
    public IOpenAICompatibleLlmService GetOpenAICompatibleService()
    {
        return _serviceProvider.GetRequiredService<IOpenAICompatibleLlmService>();
    }
}
