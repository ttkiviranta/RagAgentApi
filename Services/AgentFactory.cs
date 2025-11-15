using RagAgentApi.Models.PostgreSQL;
using RagAgentApi.Agents;
using System.Text.Json;

namespace RagAgentApi.Services;

/// <summary>
/// Factory for creating agent instances dynamically based on configuration
/// </summary>
public class AgentFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentFactory> _logger;

    // Registry of available agent types
    private readonly Dictionary<string, Type> _agentRegistry;

    public AgentFactory(IServiceProvider serviceProvider, ILogger<AgentFactory> logger)
    {
        _serviceProvider = serviceProvider;
    _logger = logger;

        // Register all available agent types
      _agentRegistry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
    {
// Built-in agents
    { "ScraperAgent", typeof(ScraperAgent) },
     { "ChunkerAgent", typeof(ChunkerAgent) },
            { "EmbeddingAgent", typeof(EmbeddingAgent) },
   { "StorageAgent", typeof(StorageAgent) },
          { "PostgresStorageAgent", typeof(PostgresStorageAgent) },
            { "QueryAgent", typeof(QueryAgent) },
     { "PostgresQueryAgent", typeof(PostgresQueryAgent) },

      // Specialized agents
       { "GitHubApiAgent", typeof(GitHubApiAgent) },
          { "YouTubeTranscriptAgent", typeof(YouTubeTranscriptAgent) },
            { "ArxivScraperAgent", typeof(ArxivScraperAgent) },
            { "NewsArticleScraperAgent", typeof(NewsArticleScraperAgent) }
        };
    }

    /// <summary>
    /// Create an agent instance by name
    /// </summary>
    /// <param name="agentName">Name of the agent to create</param>
    /// <param name="config">Optional configuration for the agent</param>
    /// <returns>Agent instance</returns>
    public BaseRagAgent CreateAgent(string agentName, JsonDocument? config = null)
    {
        try
{
  _logger.LogDebug("[AgentFactory] Creating agent: {AgentName}", agentName);

            if (!_agentRegistry.TryGetValue(agentName, out var agentType))
     {
       throw new ArgumentException($"Unknown agent type: {agentName}. Available agents: {string.Join(", ", _agentRegistry.Keys)}");
            }

            // Use DI container to create the agent instance
         var agent = (BaseRagAgent)_serviceProvider.GetRequiredService(agentType);

            if (agent == null)
     {
      throw new InvalidOperationException($"Failed to create agent instance for: {agentName}");
   }

            // Apply configuration if provided
            if (config != null)
     {
    ApplyConfiguration(agent, config);
         }

  _logger.LogDebug("[AgentFactory] Successfully created agent: {AgentName} (Type: {Type})", 
     agentName, agentType.Name);

            return agent;
        }
        catch (Exception ex)
   {
      _logger.LogError(ex, "[AgentFactory] Failed to create agent: {AgentName}", agentName);
     throw;
        }
    }

    /// <summary>
    /// Create a pipeline of agents based on agent type configuration
    /// </summary>
 /// <param name="agentType">Agent type with pipeline configuration</param>
    /// <returns>List of agent instances in pipeline order</returns>
    public List<BaseRagAgent> CreatePipeline(AgentType agentType)
    {
   try
    {
            _logger.LogDebug("[AgentFactory] Creating pipeline for agent type: {AgentTypeName}", agentType.Name);

            var pipeline = new List<BaseRagAgent>();

         // Parse agent pipeline from JSON
var pipelineConfig = ParsePipelineConfig(agentType.AgentPipeline);

   foreach (var agentConfig in pipelineConfig)
       {
         var agent = CreateAgent(agentConfig.Name, agentConfig.Config);
  pipeline.Add(agent);
}

     _logger.LogInformation("[AgentFactory] Created pipeline with {Count} agents for type: {AgentTypeName}", 
      pipeline.Count, agentType.Name);

 return pipeline;
        }
        catch (Exception ex)
        {
    _logger.LogError(ex, "[AgentFactory] Failed to create pipeline for agent type: {AgentTypeName}", 
                agentType.Name);
  throw;
        }
    }

    /// <summary>
    /// Get all registered agent types
    /// </summary>
    /// <returns>Dictionary of agent name to type</returns>
    public Dictionary<string, Type> GetRegisteredAgents()
    {
        return new Dictionary<string, Type>(_agentRegistry);
    }

    /// <summary>
    /// Register a new agent type
    /// </summary>
    /// <param name="name">Agent name</param>
    /// <param name="agentType">Agent type</param>
    public void RegisterAgent(string name, Type agentType)
    {
        if (!typeof(BaseRagAgent).IsAssignableFrom(agentType))
      {
         throw new ArgumentException($"Agent type must inherit from BaseRagAgent: {agentType.Name}");
        }

   _agentRegistry[name] = agentType;
_logger.LogInformation("[AgentFactory] Registered new agent type: {Name} -> {Type}", name, agentType.Name);
    }

    /// <summary>
    /// Test if an agent can be created
    /// </summary>
 /// <param name="agentName">Agent name to test</param>
    /// <returns>True if agent can be created</returns>
    public bool CanCreateAgent(string agentName)
    {
        return _agentRegistry.ContainsKey(agentName);
    }

    /// <summary>
    /// Get agent capabilities and metadata
    /// </summary>
    public AgentFactoryInfo GetFactoryInfo()
    {
      return new AgentFactoryInfo
     {
 RegisteredAgents = _agentRegistry.Keys.ToList(),
AgentDetails = _agentRegistry.Select(kvp => new AgentRegistryInfo
    {
     Name = kvp.Key,
            Type = kvp.Value.Name,
      Namespace = kvp.Value.Namespace ?? "",
   Category = DetermineAgentCategory(kvp.Key),
IsBuiltIn = IsBuiltInAgent(kvp.Key),
                SupportsConfiguration = SupportsConfiguration(kvp.Value)
            }).ToList(),
 TotalAgents = _agentRegistry.Count
   };
    }

    private static List<AgentPipelineStep> ParsePipelineConfig(JsonDocument pipelineDocument)
    {
        var steps = new List<AgentPipelineStep>();

     if (pipelineDocument.RootElement.ValueKind == JsonValueKind.Array)
  {
            foreach (var element in pipelineDocument.RootElement.EnumerateArray())
  {
  if (element.ValueKind == JsonValueKind.String)
         {
            // Simple string agent name
        steps.Add(new AgentPipelineStep { Name = element.GetString() ?? "" });
 }
   else if (element.ValueKind == JsonValueKind.Object)
        {
   // Object with name and config
             var step = new AgentPipelineStep();
                 
     if (element.TryGetProperty("name", out var nameElement))
            {
   step.Name = nameElement.GetString() ?? "";
         }
      
       if (element.TryGetProperty("config", out var configElement))
   {
       step.Config = JsonDocument.Parse(configElement.GetRawText());
      }

         steps.Add(step);
  }
            }
      }

        return steps;
    }

    private static void ApplyConfiguration(BaseRagAgent agent, JsonDocument config)
    {
        // TODO: Implement configuration application
        // This would typically set properties on the agent based on the config
        // For now, we'll log that configuration was provided but not applied
        // In a full implementation, you'd use reflection or a more sophisticated
        // configuration system to apply these settings
    }

    private static string DetermineAgentCategory(string agentName)
    {
        return agentName.ToLowerInvariant() switch
 {
         var name when name.Contains("scraper") => "Content Extraction",
var name when name.Contains("chunker") => "Text Processing",
       var name when name.Contains("embedding") => "AI Processing",
     var name when name.Contains("storage") => "Data Storage",
 var name when name.Contains("query") => "Query Processing",
         var name when name.Contains("github") => "Specialized - GitHub",
            var name when name.Contains("youtube") => "Specialized - YouTube",
    var name when name.Contains("arxiv") => "Specialized - Academic",
            var name when name.Contains("news") => "Specialized - News",
         _ => "General"
        };
    }

    private static bool IsBuiltInAgent(string agentName)
    {
     var builtInAgents = new[] 
        { 
         "ScraperAgent", "ChunkerAgent", "EmbeddingAgent", 
          "StorageAgent", "PostgresStorageAgent", "QueryAgent", "PostgresQueryAgent" 
 };
        return builtInAgents.Contains(agentName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool SupportsConfiguration(Type agentType)
    {
        // Check if the agent has configuration properties or a constructor that accepts configuration
   // This is a simplified check - in a real implementation you'd check for specific interfaces
        // or attributes that indicate configuration support
        return agentType.GetConstructors()
    .Any(c => c.GetParameters().Length > 1); // More than just logger parameter
    }
}

// Helper classes for pipeline configuration
public class AgentPipelineStep
{
    public string Name { get; set; } = "";
    public JsonDocument? Config { get; set; }
}

// DTOs for API responses
public class AgentFactoryInfo
{
    public List<string> RegisteredAgents { get; set; } = new();
    public List<AgentRegistryInfo> AgentDetails { get; set; } = new();
    public int TotalAgents { get; set; }
}

public class AgentRegistryInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public bool SupportsConfiguration { get; set; }
}