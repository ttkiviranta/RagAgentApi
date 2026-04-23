using RagAgentApi.Models;
using RagAgentApi.Services;
using RagAgentApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace RagAgentApi.Agents;

/// <summary>
/// Enhanced orchestrator agent that uses dynamic agent selection and specialized agents
/// </summary>
public class OrchestratorAgent : BaseRagAgent
{
    private readonly AgentSelectorService _agentSelectorService;
    private readonly AgentFactory _agentFactory;
    private readonly RagDbContext _context;
    private readonly ITelemetryService _telemetry;

    public OrchestratorAgent(
        AgentSelectorService agentSelectorService,
        AgentFactory agentFactory,
        RagDbContext context,
        ILogger<OrchestratorAgent> logger,
        ITelemetryService telemetry,
        IErrorLogService? errorLogService = null) : base(logger, errorLogService)
    {
        _agentSelectorService = agentSelectorService;
        _agentFactory = agentFactory;
        _context = context;
        _telemetry = telemetry;
    }

    public override string Name => "OrchestratorAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

        try
        {
            // Validate required data in context
            if (!context.State.TryGetValue("url", out var urlObj) || urlObj is not string url)
            {
                return AgentResult.CreateFailure("URL not found in context state");
            }

            _logger.LogInformation("[OrchestratorAgent] Starting processing for URL: {Url}", url);

            // Step 1: Select agent type
            var agentType = await _agentSelectorService.SelectAgentTypeAsync(url, cancellationToken);

            _logger.LogInformation("[OrchestratorAgent] Selected agent type: {AgentTypeName} for URL: {Url}",
                agentType.Name, url);

            // Step 2: Create pipeline
            var pipeline = _agentFactory.CreatePipeline(agentType);

            _logger.LogInformation("[OrchestratorAgent] Created pipeline with {PipelineLength} agents: {AgentNames}",
                pipeline.Count,
                string.Join(" -> ", pipeline.Select(a => a.Name)));

            // Step 3: Log pipeline start
            var executionId = await LogPipelineStartAsync(Guid.Parse(context.ThreadId), agentType, pipeline, url, cancellationToken);

            // Step 4: Execute pipeline
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
                    // Log agent execution start
                    var agentExecutionId = await LogAgentExecutionStartAsync(
                        executionId, agent.Name, currentContext, cancellationToken);

                    var swAgent = System.Diagnostics.Stopwatch.StartNew();
                    var agentResult = await agent.ExecuteAsync(currentContext, cancellationToken);
                    swAgent.Stop();

                    // Telemetry: track agent execution latency and selected agent
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
                    catch { /* ignore telemetry failures */ }
                    pipelineResults.Add(agentResult);

                    // Log agent execution completion
                    await LogAgentExecutionCompleteAsync(
                        agentExecutionId, agentResult, currentContext, cancellationToken);

                    // Handle failure
                    if (!agentResult.Success)
                    {
                        _logger.LogError("[OrchestratorAgent] Pipeline failed at step {Step}: {AgentName} - {Error}",
                            stepNumber, agent.Name, agentResult.Message);

                        // 🔥 Log pipeline error to ErrorLogService
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

                    // 🔥 Log exception to ErrorLogService
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

            stopwatch.Stop();

            // Step 5: Log success
            await LogPipelineCompleteAsync(executionId, true, "Pipeline completed successfully",
                pipelineResults, cancellationToken);

            // Step 6: Final result
            var finalResult = CreateFinalResult(agentType, pipeline, pipelineResults, currentContext, stopwatch);

            LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

            _logger.LogInformation("[OrchestratorAgent] Pipeline completed successfully for URL: {Url} in {Duration}ms",
                url, stopwatch.ElapsedMilliseconds);

            AddMessage(currentContext, "System", "Pipeline executed successfully",
                new Dictionary<string, object>
                {
                    { "agent_type", agentType.Name },
                    { "pipeline_length", pipeline.Count },
                    { "total_time_ms", stopwatch.ElapsedMilliseconds },
                    { "url", url }
                });

            return finalResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
            return HandleException(ex, context.ThreadId, "orchestrated pipeline execution");
        }
    }

    // --------------------------------------------------------------------
    // Logging helpers (unchanged)
    // --------------------------------------------------------------------

    private async Task<Guid> LogPipelineStartAsync(
        Guid threadId,
        Models.PostgreSQL.AgentType agentType,
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
                    url = url,
                    pipeline_agents = pipeline.Select(a => a.Name).ToList(),
                    pipeline_length = pipeline.Count
                }))
            };

            _context.AgentExecutions.Add(execution);
            await _context.SaveChangesAsync(cancellationToken);

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

            _context.AgentExecutions.Add(execution);
            await _context.SaveChangesAsync(cancellationToken);

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
            var execution = await _context.AgentExecutions
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

                await _context.SaveChangesAsync(cancellationToken);
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
            var execution = await _context.AgentExecutions
                .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

            if (execution != null)
            {
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationMs = (int)(execution.CompletedAt.Value - execution.StartedAt).TotalMilliseconds;
                execution.Status = success ? "success" : "failed";
                execution.ErrorMessage = success ? null : message;
                execution.OutputData = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    success = success,
                    message = message,
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

                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrchestratorAgent] Failed to log pipeline completion for {ExecutionId}", executionId);
        }
    }

    private static AgentResult CreateFinalResult(
        Models.PostgreSQL.AgentType agentType,
        List<BaseRagAgent> pipeline,
        List<AgentResult> pipelineResults,
        AgentContext context,
        System.Diagnostics.Stopwatch stopwatch)
    {
        var metrics = new Dictionary<string, object>
        {
            { "agent_type", agentType.Name },
            { "pipeline_agents", pipeline.Select(a => a.Name).ToList() },
            { "pipeline_length", pipeline.Count },
            { "total_execution_time_ms", stopwatch.ElapsedMilliseconds },
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