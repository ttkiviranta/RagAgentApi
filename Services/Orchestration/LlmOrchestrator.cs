using Microsoft.EntityFrameworkCore;
using RagAgentApi.Agents;
using RagAgentApi.Data;
using RagAgentApi.Models;
using RagAgentApi.Models.PostgreSQL;
using System.Text.Json;

namespace RagAgentApi.Services.Orchestration;

public interface ILlmOrchestrator
{
    Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default);
}

public class LlmOrchestrator : ILlmOrchestrator
{
    private readonly AgentSelectorService _agentSelectorService;
    private readonly AgentFactory _agentFactory;
    private readonly ILlmPlannerService _plannerService;
    private readonly RagDbContext _dbContext;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<LlmOrchestrator> _logger;
    private readonly IErrorLogService? _errorLogService;

    public LlmOrchestrator(
        AgentSelectorService agentSelectorService,
        AgentFactory agentFactory,
        ILlmPlannerService plannerService,
        RagDbContext dbContext,
        ITelemetryService telemetry,
        ILogger<LlmOrchestrator> logger,
        IErrorLogService? errorLogService = null)
    {
        _agentSelectorService = agentSelectorService;
        _agentFactory = agentFactory;
        _plannerService = plannerService;
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

        _logger.LogInformation("[OrchestratorAgent] Starting LLM-driven processing for URL: {Url}", url);

        var agentType = await _agentSelectorService.SelectAgentTypeAsync(url, cancellationToken);
        var pipeline = _agentFactory.CreatePipeline(agentType);

        var availableAgentNames = pipeline.Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (availableAgentNames.Count == 0)
        {
            return AgentResult.CreateFailure("No available agents found for selected agent type pipeline.");
        }

        var executionId = await LogPipelineStartAsync(Guid.Parse(context.ThreadId), agentType, availableAgentNames, url, cancellationToken);

        var pipelineResults = new List<AgentResult>();
        var llmSteps = new List<LlmExecutionStep>();
        var maxSteps = Math.Max(availableAgentNames.Count * 3, 6);

        for (int step = 1; step <= maxSteps; step++)
        {
            var decision = await _plannerService.DecideNextAgentAsync(context, availableAgentNames, llmSteps, cancellationToken);

            if (decision.Done)
            {
                await LogPipelineCompleteAsync(executionId, true, decision.FinalMessage ?? "LLM orchestration completed", pipelineResults, cancellationToken);
                AddMessage(context, "System", "LLM orchestration completed",
                    new Dictionary<string, object>
                    {
                        { "agent_type", agentType.Name },
                        { "steps_executed", llmSteps.Count },
                        { "url", url }
                    });

                return CreateFinalResult(agentType, pipelineResults, llmSteps, context, decision.FinalMessage);
            }

            var nextAgentName = ResolveNextAgentName(decision.NextAgent, availableAgentNames);
            var nextAgent = _agentFactory.CreateAgent(nextAgentName);

            _logger.LogInformation("[OrchestratorAgent] LLM selected step {Step}/{MaxSteps}: {AgentName}. Reason: {Reason}",
                step, maxSteps, nextAgentName, decision.Reason);

            try
            {
                var agentExecutionId = await LogAgentExecutionStartAsync(executionId, nextAgent.Name, context, cancellationToken);

                var swAgent = System.Diagnostics.Stopwatch.StartNew();
                var agentResult = await nextAgent.ExecuteAsync(context, cancellationToken);
                swAgent.Stop();

                try
                {
                    var props = new Dictionary<string, string>
                    {
                        { "agent_name", nextAgent.Name },
                        { "selected_agent", nextAgent.Name },
                        { "orchestration_mode", "LLM" },
                        { "retrieval_mode", context.State.GetValueOrDefault("retrieval_mode")?.ToString() ?? "unknown" }
                    };

                    _telemetry.TrackMetric("llm_call_latency_ms", swAgent.ElapsedMilliseconds, props);
                }
                catch
                {
                }

                pipelineResults.Add(agentResult);

                await LogAgentExecutionCompleteAsync(agentExecutionId, agentResult, context, cancellationToken);

                var executedStep = new LlmExecutionStep
                {
                    Step = step,
                    AgentName = nextAgent.Name,
                    Success = agentResult.Success,
                    Message = agentResult.Message
                };
                llmSteps.Add(executedStep);

                if (!agentResult.Success)
                {
                    _logger.LogError("[OrchestratorAgent] LLM pipeline failed at step {Step}: {AgentName} - {Error}",
                        step, nextAgent.Name, agentResult.Message);

                    if (_errorLogService != null)
                    {
                        _ = _errorLogService.LogErrorAsync(
                            message: $"LLM pipeline failed at step {step} ({nextAgent.Name}): {agentResult.Message}",
                            category: "Pipeline",
                            severity: "ERROR",
                            operationName: nextAgent.Name,
                            requestId: context.ThreadId
                        );
                    }

                    await LogPipelineCompleteAsync(executionId, false,
                        $"LLM pipeline failed at {nextAgent.Name}: {agentResult.Message}",
                        pipelineResults, cancellationToken);

                    return AgentResult.CreateFailure(
                        $"LLM orchestration failed at step {step} ({nextAgent.Name}): {agentResult.Message}",
                        agentResult.Errors);
                }

                var evaluation = await _plannerService.EvaluateStepAsync(context, executedStep, availableAgentNames, llmSteps, cancellationToken);
                if (evaluation.Done)
                {
                    await LogPipelineCompleteAsync(executionId, true, "LLM orchestration completed", pipelineResults, cancellationToken);
                    AddMessage(context, "System", "LLM orchestration completed", new Dictionary<string, object>
                    {
                        { "agent_type", agentType.Name },
                        { "steps_executed", llmSteps.Count },
                        { "url", url }
                    });

                    return CreateFinalResult(agentType, pipelineResults, llmSteps, context, evaluation.Reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OrchestratorAgent] Exception in LLM orchestration step {Step}: {AgentName}",
                    step, nextAgentName);

                if (_errorLogService != null)
                {
                    _ = _errorLogService.LogErrorAsync(
                        message: $"LLM pipeline exception at step {step} ({nextAgentName}): {ex.Message}",
                        category: "Pipeline",
                        severity: "ERROR",
                        operationName: nextAgentName,
                        requestId: context.ThreadId
                    );
                }

                await LogPipelineCompleteAsync(executionId, false,
                    $"LLM pipeline exception at {nextAgentName}: {ex.Message}",
                    pipelineResults, cancellationToken);

                return AgentResult.CreateFailure(
                    $"LLM orchestration failed at step {step} ({nextAgentName}): {ex.Message}");
            }
        }

        await LogPipelineCompleteAsync(executionId, false,
            "LLM orchestration reached maximum step limit",
            pipelineResults, cancellationToken);

        return AgentResult.CreateFailure("LLM orchestration reached maximum step limit.");
    }

    private static string ResolveNextAgentName(string? candidate, IReadOnlyList<string> availableAgents)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = availableAgents.FirstOrDefault(a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match) && !string.Equals(match, "OrchestratorAgent", StringComparison.OrdinalIgnoreCase))
            {
                return match;
            }
        }

        return availableAgents.First(a => !string.Equals(a, "OrchestratorAgent", StringComparison.OrdinalIgnoreCase));
    }

    private void AddMessage(AgentContext context, string to, string content, Dictionary<string, object>? data = null)
    {
        var message = new AgentMessage
        {
            From = nameof(LlmOrchestrator),
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
        IReadOnlyList<string> availableAgents,
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            var execution = new Models.PostgreSQL.AgentExecution
            {
                ThreadId = threadId,
                AgentName = $"LlmPipeline_{agentType.Name}",
                StartedAt = DateTime.UtcNow,
                Status = "running",
                InputData = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    orchestration_mode = "LLM",
                    agent_type = agentType.Name,
                    url,
                    pipeline_agents = availableAgents.ToList()
                }))
            };

            _dbContext.AgentExecutions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return execution.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrchestratorAgent] Failed to log LLM pipeline start, continuing without logging");
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
        IReadOnlyList<AgentResult> pipelineResults,
        IReadOnlyList<LlmExecutionStep> llmSteps,
        AgentContext context,
        string? completionReason)
    {
        var metrics = new Dictionary<string, object>
        {
            { "orchestration_mode", "LLM" },
            { "agent_type", agentType.Name },
            { "steps_executed", llmSteps.Count },
            { "all_steps_successful", pipelineResults.All(r => r.Success) },
            { "successful_steps", pipelineResults.Count(r => r.Success) },
            { "completion_reason", completionReason ?? string.Empty },
            { "step_results", llmSteps.Select(s => new
                {
                    step = s.Step,
                    agent = s.AgentName,
                    success = s.Success,
                    message = s.Message
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
            $"LLM orchestration completed successfully using {agentType.Name}",
            metrics);
    }
}
