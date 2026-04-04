using Microsoft.Extensions.Options;

namespace RagAgentApi.Services.Retrieval;

/// <summary>
/// Factory for creating and selecting retrieval strategies based on configuration.
/// </summary>
public class RetrievalStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RetrievalSettings _settings;
    private readonly ILogger<RetrievalStrategyFactory> _logger;

    public RetrievalStrategyFactory(
        IServiceProvider serviceProvider,
        IOptions<RetrievalSettings> settings,
        ILogger<RetrievalStrategyFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets the configured retrieval mode
    /// </summary>
    public string ConfiguredMode => _settings.Mode;

    /// <summary>
    /// Gets the retrieval strategy based on the configured mode
    /// </summary>
    public IRetrievalStrategy GetStrategy()
    {
        IRetrievalStrategy strategy = _settings.Mode?.ToLowerInvariant() switch
        {
            "filefirst" => _serviceProvider.GetRequiredService<FileFirstRetrievalStrategy>(),
            "auto" => _serviceProvider.GetRequiredService<AutoRetrievalStrategy>(),
            _ => _serviceProvider.GetRequiredService<RagRetrievalStrategy>() // Default to RAG
        };

        _logger.LogDebug("[RetrievalStrategyFactory] Returning strategy: {Strategy} for mode: {Mode}", 
            strategy.Name, _settings.Mode);

        return strategy;
    }

    /// <summary>
    /// Gets a specific strategy by name (overrides configuration)
    /// </summary>
    public IRetrievalStrategy GetStrategy(string strategyName)
    {
        IRetrievalStrategy strategy = strategyName?.ToLowerInvariant() switch
        {
            "filefirst" => _serviceProvider.GetRequiredService<FileFirstRetrievalStrategy>(),
            "auto" => _serviceProvider.GetRequiredService<AutoRetrievalStrategy>(),
            "rag" => _serviceProvider.GetRequiredService<RagRetrievalStrategy>(),
            _ => GetStrategy() // Fall back to configured strategy
        };
        return strategy;
    }
}
