using RagAgentApi.Services.A2A;

namespace RagAgentApi.Services.DemoServices;

/// <summary>
/// Demonstrates Agent-to-Agent communication in the RAG pipeline
/// </summary>
public class A2ADemoService : IDemoService
{
    private readonly IA2AProtocolService _protocolService;
    private readonly ILogger<A2ADemoService> _logger;

    public A2ADemoService(IA2AProtocolService protocolService, ILogger<A2ADemoService> logger)
    {
        _protocolService = protocolService;
        _logger = logger;
    }

    public async Task GenerateTestDataAsync()
    {
        _logger.LogInformation("[A2ADemo] Generating test data...");
        // A2A demo doesn't require test data generation
        await Task.CompletedTask;
    }

    public async Task<DemoResult> RunDemoAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var conversationId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("[A2ADemo] Starting Agent-to-Agent pipeline demonstration");

            var pipelineSteps = new List<A2APipelineStep>();

            // Step 1: Orchestrator → Scraper
            var step1 = await ExecutePipelineStep(
                conversationId,
                "OrchestratorAgent",
                "ScraperAgent",
                "scrape",
                "Extract content from: https://github.com/microsoft/semantic-kernel",
                new { url = "https://github.com/microsoft/semantic-kernel", timeout = 5000 }
            );
            pipelineSteps.Add(step1);

            // Step 2: Scraper → Chunker
            var step2 = await ExecutePipelineStep(
                conversationId,
                "ScraperAgent",
                "ChunkerAgent",
                "chunk",
                "Split content into semantic chunks",
                new { chunkSize = 1000, chunkOverlap = 200 }
            );
            pipelineSteps.Add(step2);

            // Step 3: Chunker → Embedding
            var step3 = await ExecutePipelineStep(
                conversationId,
                "ChunkerAgent",
                "EmbeddingAgent",
                "embed",
                "Generate embeddings for chunks",
                new { model = "text-embedding-ada-002", dimensions = 1536 }
            );
            pipelineSteps.Add(step3);

            // Step 4: Embedding → Storage
            var step4 = await ExecutePipelineStep(
                conversationId,
                "EmbeddingAgent",
                "PostgresStorageAgent",
                "store",
                "Store documents and embeddings",
                new { database = "PostgreSQL", indexType = "IVFFlat" }
            );
            pipelineSteps.Add(step4);

            // Step 5: Parallel Query Agent
            var step5 = await ExecutePipelineStep(
                conversationId,
                "OrchestratorAgent",
                "PostgresQueryAgent",
                "query",
                "Search for similar documents",
                new { query = "What is Semantic Kernel?", topK = 5 }
            );
            pipelineSteps.Add(step5);

            stopwatch.Stop();

            var result = new DemoResult
            {
                DemoType = "a2a-pipeline",
                Success = true,
                Message = "Agent-to-Agent pipeline executed successfully",
                Data = new
                {
                    conversationId,
                    totalSteps = pipelineSteps.Count,
                    totalMessages = pipelineSteps.SelectMany(s => s.Messages).Count(),
                    pipelineSteps = pipelineSteps.Select(step => new
                    {
                        step.StepNumber,
                        step.FromAgent,
                        step.ToAgent,
                        step.MessageType,
                        step.Description,
                        step.StatusCode,
                        step.ExecutionTimeMs,
                        messageCount = step.Messages.Count
                    }).ToList(),
                    summary = new
                    {
                        totalExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                        averageStepTimeMs = pipelineSteps.Average(s => s.ExecutionTimeMs),
                        agentsInvolved = pipelineSteps.SelectMany(s => new[] { s.FromAgent, s.ToAgent }).Distinct().Count()
                    }
                },
                ExecutionTimeMs = $"{stopwatch.ElapsedMilliseconds}ms"
            };

            _logger.LogInformation($"[A2ADemo] Pipeline completed in {stopwatch.ElapsedMilliseconds}ms");
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError($"[A2ADemo] Error: {ex.Message}");

            return new DemoResult
            {
                DemoType = "a2a-pipeline",
                Success = false,
                Message = $"Error: {ex.Message}",
                ExecutionTimeMs = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
    }

    private async Task<A2APipelineStep> ExecutePipelineStep(
        string conversationId,
        string fromAgent,
        string toAgent,
        string messageType,
        string description,
        object payload)
    {
        var step = new A2APipelineStep
        {
            FromAgent = fromAgent,
            ToAgent = toAgent,
            MessageType = messageType,
            Description = description
        };

        try
        {
            // Get agent IDs
            var agents = await _protocolService.GetRegisteredAgentsAsync();
            var fromAgentObj = agents.FirstOrDefault(a => a.Name == fromAgent);
            var toAgentObj = agents.FirstOrDefault(a => a.Name == toAgent);

            if (fromAgentObj == null || toAgentObj == null)
            {
                throw new InvalidOperationException($"Agent not found: {fromAgent} or {toAgent}");
            }

            // Create message
            var message = new A2AMessage
            {
                ConversationId = conversationId,
                FromAgentId = fromAgentObj.Id,
                FromAgentName = fromAgent,
                ToAgentId = toAgentObj.Id,
                ToAgentName = toAgent,
                MessageType = messageType,
                Content = description,
                Payload = payload
            };

            // Send message
            var response = await _protocolService.SendMessageAsync(message);

            step.Messages.Add(response);
            step.StatusCode = response.Status == A2AMessageStatus.Completed ? 200 : 500;
            step.ExecutionTimeMs = response.ExecutionTimeMs;
        }
        catch (Exception ex)
        {
            step.StatusCode = 500;
            step.ExecutionTimeMs = 0;
            _logger.LogError($"[A2ADemo] Step error: {ex.Message}");
        }

        return step;
    }
}

/// <summary>
/// Represents a step in the A2A pipeline
/// </summary>
public class A2APipelineStep
{
    public int StepNumber { get; set; }
    public string FromAgent { get; set; } = string.Empty;
    public string ToAgent { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int StatusCode { get; set; } = 200;
    public long ExecutionTimeMs { get; set; } = 0;
    public List<A2AMessage> Messages { get; set; } = new();
}
