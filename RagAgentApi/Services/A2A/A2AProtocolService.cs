namespace RagAgentApi.Services.A2A;

/// <summary>
/// Agent-to-Agent communication protocol service
/// Manages message passing and protocol compliance between agents
/// </summary>
public interface IA2AProtocolService
{
    /// <summary>
    /// Send a message from one agent to another
    /// </summary>
    Task<A2AMessage> SendMessageAsync(A2AMessage message);

    /// <summary>
    /// Get message history for a conversation
    /// </summary>
    Task<List<A2AMessage>> GetMessageHistoryAsync(string conversationId);

    /// <summary>
    /// Register an agent in the protocol
    /// </summary>
    Task RegisterAgentAsync(A2AAgent agent);

    /// <summary>
    /// Get all registered agents
    /// </summary>
    Task<List<A2AAgent>> GetRegisteredAgentsAsync();
}

/// <summary>
/// Represents an agent in the A2A protocol
/// </summary>
public class A2AAgent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a message in A2A protocol
/// </summary>
public class A2AMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public string FromAgentId { get; set; } = string.Empty;
    public string FromAgentName { get; set; } = string.Empty;
    public string ToAgentId { get; set; } = string.Empty;
    public string ToAgentName { get; set; } = string.Empty;
    public string MessageType { get; set; } = "task";
    public string Content { get; set; } = string.Empty;
    public A2AMessageStatus Status { get; set; } = A2AMessageStatus.Pending;
    public object? Payload { get; set; }
    public object? Response { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public long ExecutionTimeMs { get; set; } = 0;
}

/// <summary>
/// Message status in A2A protocol
/// </summary>
public enum A2AMessageStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Timeout
}

/// <summary>
/// Default implementation of A2A protocol service
/// </summary>
public class A2AProtocolService : IA2AProtocolService
{
    private readonly Dictionary<string, A2AAgent> _agents = new();
    private readonly Dictionary<string, List<A2AMessage>> _conversations = new();
    private readonly ILogger<A2AProtocolService> _logger;

    public A2AProtocolService(ILogger<A2AProtocolService> logger)
    {
        _logger = logger;
        InitializeDefaultAgents();
    }

    public async Task<A2AMessage> SendMessageAsync(A2AMessage message)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation($"[A2A] Message from {message.FromAgentName} to {message.ToAgentName}: {message.MessageType}");

            // Validate agents exist
            if (!_agents.ContainsKey(message.FromAgentId))
            {
                throw new InvalidOperationException($"Agent {message.FromAgentName} not registered");
            }

            if (!_agents.ContainsKey(message.ToAgentId))
            {
                throw new InvalidOperationException($"Agent {message.ToAgentName} not registered");
            }

            // Add to conversation history
            if (!_conversations.ContainsKey(message.ConversationId))
            {
                _conversations[message.ConversationId] = new List<A2AMessage>();
            }

            message.Status = A2AMessageStatus.Processing;
            _conversations[message.ConversationId].Add(message);

            // Simulate processing
            await Task.Delay(100);

            message.Status = A2AMessageStatus.Completed;
            message.ProcessedAt = DateTime.UtcNow;

            stopwatch.Stop();
            message.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation($"[A2A] Message processed: {message.Id} ({message.ExecutionTimeMs}ms)");

            return message;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            message.Status = A2AMessageStatus.Failed;
            message.ErrorMessage = ex.Message;
            message.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            message.ProcessedAt = DateTime.UtcNow;

            _logger.LogError($"[A2A] Message failed: {ex.Message}");
            throw;
        }
    }

    public async Task<List<A2AMessage>> GetMessageHistoryAsync(string conversationId)
    {
        if (_conversations.ContainsKey(conversationId))
        {
            return await Task.FromResult(_conversations[conversationId]);
        }
        return await Task.FromResult(new List<A2AMessage>());
    }

    public async Task RegisterAgentAsync(A2AAgent agent)
    {
        if (_agents.ContainsKey(agent.Id))
        {
            _logger.LogWarning($"[A2A] Agent {agent.Name} already registered");
            return;
        }

        _agents[agent.Id] = agent;
        _logger.LogInformation($"[A2A] Agent registered: {agent.Name} ({agent.Type})");
        await Task.CompletedTask;
    }

    public async Task<List<A2AAgent>> GetRegisteredAgentsAsync()
    {
        return await Task.FromResult(_agents.Values.ToList());
    }

    private void InitializeDefaultAgents()
    {
        var defaultAgents = new[]
        {
            new A2AAgent
            {
                Name = "OrchestratorAgent",
                Type = "coordinator",
                Description = "Orchestrates pipeline and routes tasks",
                Capabilities = new() { "routing", "orchestration", "task-distribution" }
            },
            new A2AAgent
            {
                Name = "ScraperAgent",
                Type = "processor",
                Description = "Scrapes and extracts content from URLs",
                Capabilities = new() { "web-scraping", "content-extraction", "html-parsing" }
            },
            new A2AAgent
            {
                Name = "ChunkerAgent",
                Type = "processor",
                Description = "Splits content into semantic chunks",
                Capabilities = new() { "text-splitting", "sentence-boundary-detection", "overlap-management" }
            },
            new A2AAgent
            {
                Name = "EmbeddingAgent",
                Type = "processor",
                Description = "Generates vector embeddings",
                Capabilities = new() { "embedding-generation", "vector-creation", "dimension-reduction" }
            },
            new A2AAgent
            {
                Name = "PostgresStorageAgent",
                Type = "storage",
                Description = "Stores documents and embeddings in PostgreSQL",
                Capabilities = new() { "persistence", "indexing", "query-optimization" }
            },
            new A2AAgent
            {
                Name = "PostgresQueryAgent",
                Type = "storage",
                Description = "Performs semantic search on stored vectors",
                Capabilities = new() { "vector-search", "similarity-matching", "metadata-filtering" }
            }
        };

        foreach (var agent in defaultAgents)
        {
            _agents[agent.Id] = agent;
        }

        _logger.LogInformation($"[A2A] Initialized {defaultAgents.Length} default agents");
    }
}
