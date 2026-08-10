using Microsoft.EntityFrameworkCore;
using RagAgentApi.Agents;
using RagAgentApi.Data;
using RagAgentApi.Models;
using RagAgentApi.Models.PostgreSQL;
using System.Text.Json;

namespace RagAgentApi.Services.Orchestration;

public interface IPipelineOrchestrator
{
    Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default);
}

public class PipelineOrchestrator : IPipelineOrchestrator
{
    private readonly AgentSelectorService _agentSelectorService;
    private readonly AgentFactory _agentFactory;
    private readonly RagDbContext _dbContext;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<PipelineOrchestrator> _logger;
    private readonly IErrorLogService? _errorLogService;

    public PipelineOrchestrator(
        AgentSelectorService agentSelectorService,
        AgentFactory agentFactory,
        RagDbContext dbContext,
        ITelemetryService telemetry,
        ILogger<PipelineOrchestrator> logger,
        IErrorLogService? errorLogService = null)
    {
        _agentSelectorService = agentSelectorService;
        _agentFactory = agentFactory;
        _dbContext = dbContext;
        _telemetry = telemetry;
        _logger = logger;
        _errorLogService = errorLogService;
    }

    public async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        if (!context.State.TryGetValue("url", out var urlObj) || urlObj is not string url)
        {
            return AgentResult.CreateFailure("URL not found in context state");
        }

        _logger.LogInformation("[OrchestratorAgent] Starting processing for URL: {Url}", url);

        var agentType = await _agentSelectorService.SelectAgentTypeAsync(url, cancellationToken);

        _logger.LogInformation("[OrchestratorAgent] Selected agent type: {AgentTypeName} for URL: {Url}",
            agentType.Name, url);

        var pipeline = _agentFactory.CreatePipeline(agentType);

        _logger.LogInformation("[OrchestratorAgent] Created pipeline with {PipelineLength} agents: {AgentNames}",
            pipeline.Count,
            string.Join(" -> ", pipeline.Select(a => a.Name)));

        var executionId = await LogPipelineStartAsync(Guid.Parse(context.ThreadId), agentType, pipeline, url, cancellationToken);

        var pipelineResults = new List<AgentResult>();
        var currentContext = context;

        for (int i = 0; i < pipeline.Count; i++)
        {
            var agent = pipeline[i];
            var stepNumber = i + 1;

            _logger.LogInformation("[OrchestratorAgent] Executing pipeline step {Step}/{Total}: {AgentName}",
                stepNumber, pipeline.Count, agent.Name);

            try
            {
                var agentExecutionId = await LogAgentExecutionStartAsync(
                    executionId, agent.Name, currentContext, cancellationToken);

                var swAgent = System.Diagnostics.Stopwatch.StartNew();
                var agentResult = await agent.ExecuteAsync(currentContext, cancellationToken);
                swAgent.Stop();

                try
                {
                    var props = new Dictionary<string, string>
                    {
                        { "agent_name", agent.Name },
                        { "selected_agent", agent.Name },
                        { "retrieval_mode", currentContext.State.GetValueOrDefault("retrieval_mode")?.ToString() ?? "unknown" }
                    };

                    _telemetry.TrackMetric("llm_call_latency_ms", swAgent.ElapsedMilliseconds, props);
                }
                catch
                {
                }

                pipelineResults.Add(agentResult);

                await LogAgentExecutionCompleteAsync(
                    agentExecutionId, agentResult, currentContext, cancellationToken);

                if (!agentResult.Success)
                {
                    _logger.LogError("[OrchestratorAgent] Pipeline failed at step {Step}: {AgentName} - {Error}",
                        stepNumber, agent.Name, agentResult.Message);

                    if (_errorLogService != null)
                    {
                        _ = _errorLogService.LogErrorAsync(
                            message: $"Pipeline failed at step {stepNumber} ({agent.Name}): {agentResult.Message}",
                            category: "Pipeline",
                            severity: "ERROR",
                            operationName: agent.Name,
                            requestId: context.ThreadId
                        );
                    }

                    await LogPipelineCompleteAsync(executionId, false,
                        $"Pipeline failed at {agent.Name}: {agentResult.Message}",
                        pipelineResults, cancellationToken);

                    return AgentResult.CreateFailure(
                        $"Pipeline execution failed at step {stepNumber} ({agent.Name}): {agentResult.Message}",
                        agentResult.Errors);
                }

                _logger.LogDebug("[OrchestratorAgent] Step {Step} completed successfully: {AgentName}",
                    stepNumber, agent.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrchestratorAgent] Exception in pipeline step {Step}: {AgentName}",
                    stepNumber, agent.Name);

                if (_errorLogService != null)
                {
                    _ = _errorLogService.LogErrorAsync(
                        message: $"Pipeline exception at step {stepNumber} ({agent.Name}): {ex.Message}",
                        category: "Pipeline",
                        severity: "ERROR",
                        operationName: agent.Name,
                        requestId: context.ThreadId
                    );
                }

                await LogPipelineCompleteAsync(executionId, false,
                    $"Pipeline exception at {agent.Name}: {ex.Message}",
                    pipelineResults, cancellationToken);

                return AgentResult.CreateFailure(
                    $"Pipeline execution failed at step {stepNumber} ({agent.Name}): {ex.Message}");
            }
        }

        await LogPipelineCompleteAsync(executionId, true, "Pipeline completed successfully",
            pipelineResults, cancellationToken);

        var finalResult = CreateFinalResult(agentType, pipeline, pipelineResults, currentContext);

        _logger.LogInformation("[OrchestratorAgent] Pipeline completed successfully for URL: {Url}", url);

        AddMessage(currentContext, "System", "Pipeline executed successfully",
            new Dictionary<string, object>
            {
                { "agent_type", agentType.Name },
                { "pipeline_length", pipeline.Count },
                { "url", url }
            });

        return finalResult;
    }

    private void AddMessage(AgentContext context, string to, string content, Dictionary<string, object>? data = null)
    {
        var message = new AgentMessage
        {
            From = nameof(PipelineOrchestrator),
            To = to,
            Content = content,
            Data = data
        };

        context.Messages.Add(message);
        context.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task<Guid> LogPipelineStartAsync(
        Guid threadId,
        AgentType agentType,
        List<BaseRagAgent> pipeline,
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            var execution = new Models.PostgreSQL.AgentExecution
            {
                ThreadId = threadId,
                AgentName = $"Pipeline_{agentType.Name}",
                StartedAt = DateTime.UtcNow,
                Status = "running",
                InputData = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    agent_type = agentType.Name,
                    url,
                    pipeline_agents = pipeline.Select(a => a.Name).ToList(),
                    pipeline_length = pipeline.Count
                }))
            };

            _dbContext.AgentExecutions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return execution.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrchestratorAgent] Failed to log pipeline start, continuing without logging");
            return Guid.NewGuid();
        }
    }

    private async Task<Guid> LogAgentExecutionStartAsync(
        Guid parentExecutionId,
        string agentName,
        AgentContext agentExecutionContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var execution = new Models.PostgreSQL.AgentExecution
            {
                ThreadId = Guid.Parse(agentExecutionContext.ThreadId),
                AgentName = agentName,
                ParentExecutionId = parentExecutionId,
                StartedAt = DateTime.UtcNow,
                Status = "running",
                InputData = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    context_state_keys = agentExecutionContext.State.Keys.ToList(),
                    message_count = agentExecutionContext.Messages.Count
                }))
            };

            _dbContext.AgentExecutions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return execution.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrchestratorAgent] Failed to log agent execution start for {AgentName}", agentName);
            return Guid.NewGuid();
        }
    }

    private async Task LogAgentExecutionCompleteAsync(
        Guid executionId,
        AgentResult result,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var execution = await _dbContext.AgentExecutions
                .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

            if (execution != null)
            {
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationMs = (int)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds;
                execution.Status = result.Success ? "success" : "failed";
                execution.ErrorMessage = result.Success ? null : result.Message;
                execution.OutputData = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    success = result.Success,
                    message = result.Message,
                    data_keys = result.Data?.Keys.ToList() ?? new List<string>(),
                    context_state_keys = context.State.Keys.ToList(),
                    final_message_count = context.Messages.Count
                }));

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrchestratorAgent] Failed to log agent execution completion for {ExecutionId}", executionId);
        }
    }

    private async Task LogPipelineCompleteAsync(
        Guid executionId,
        bool success,
        string message,
        List<AgentResult> pipelineResults,
        CancellationToken cancellationToken)
    {
        try
        {
            var execution = await _dbContext.AgentExecutions
                .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

            if (execution != null)
            {
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationMs = (int)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds;
                execution.Status = success ? "success" : "failed";
                execution.ErrorMessage = success ? null : message;
                execution.OutputData = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    success,
                    message,
                    pipeline_results = pipelineResults.Select((r, i) => new
                    {
                        step = i + 1,
                        success = r.Success,
                        message = r.Message,
                        execution_time_ms = r.Data?.GetValueOrDefault("execution_time_ms", 0)
                    }).ToList(),
                    total_steps = pipelineResults.Count,
                    successful_steps = pipelineResults.Count(r => r.Success)
                }));

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrchestratorAgent] Failed to log pipeline completion for {ExecutionId}", executionId);
        }
    }

    private static AgentResult CreateFinalResult(
        AgentType agentType,
        List<BaseRagAgent> pipeline,
        List<AgentResult> pipelineResults,
        AgentContext context)
    {
        var metrics = new Dictionary<string, object>
        {
            { "agent_type", agentType.Name },
            { "pipeline_agents", pipeline.Select(a => a.Name).ToList() },
            { "pipeline_length", pipeline.Count },
            { "all_steps_successful", pipelineResults.All(r => r.Success) },
            { "successful_steps", pipelineResults.Count(r => r.Success) },
            { "step_results", pipelineResults.Select((r, i) => new
                {
                    step = i + 1,
                    agent = pipeline[i].Name,
                    success = r.Success,
                    message = r.Message,
                    execution_time_ms = r.Data?.GetValueOrDefault("execution_time_ms", 0)
                }).ToList()
            }
        };

        if (context.State.TryGetValue("document_id", out var documentId))
            metrics["document_id"] = documentId;

        if (context.State.TryGetValue("chunks_stored", out var chunksStored))
            metrics["chunks_stored"] = chunksStored;

        if (context.State.TryGetValue("url", out var url))
            metrics["processed_url"] = url;

        return AgentResult.CreateSuccess(
            $"Pipeline executed successfully using {agentType.Name}",
            metrics);
    }
}
