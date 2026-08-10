using Microsoft.Extensions.Options;
using RagAgentApi.Models;
using RagAgentApi.Options;
using RagAgentApi.Services;
using RagAgentApi.Services.Orchestration;

namespace RagAgentApi.Agents;

public class OrchestratorAgent : BaseRagAgent
{
    private readonly OrchestrationOptions _orchestrationOptions;
    private readonly IPipelineOrchestrator _pipelineOrchestrator;
    private readonly ILlmOrchestrator _llmOrchestrator;

    public OrchestratorAgent(
        IOptions<OrchestrationOptions> orchestrationOptions,
        IPipelineOrchestrator pipelineOrchestrator,
        ILlmOrchestrator llmOrchestrator,
        ILogger<OrchestratorAgent> logger,
        IErrorLogService? errorLogService = null)
        : base(logger, errorLogService)
    {
        _orchestrationOptions = orchestrationOptions.Value;
        _pipelineOrchestrator = pipelineOrchestrator;
        _llmOrchestrator = llmOrchestrator;
    }

    public override string Name => "OrchestratorAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

        var mode = _orchestrationOptions.Mode;

        try
        {
            _logger.LogInformation("[OrchestratorAgent] Running orchestration mode: {Mode}", mode);

            AgentResult result = mode switch
            {
                OrchestrationMode.LLM => await _llmOrchestrator.ExecuteAsync(context, cancellationToken),
                OrchestrationMode.Pipeline => await _pipelineOrchestrator.ExecuteAsync(context, cancellationToken),
                _ => await _pipelineOrchestrator.ExecuteAsync(context, cancellationToken)
            };

            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, result.Success, stopwatch.Elapsed);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
            return HandleException(ex, context.ThreadId, $"orchestration execution ({mode})");
        }
    }
}
