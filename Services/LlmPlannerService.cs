using System.Text;
using System.Text.Json;
using RagAgentApi.Models;

namespace RagAgentApi.Services;

public interface ILlmPlannerService
{
    Task<LlmPlannerDecision> DecideNextAgentAsync(
        AgentContext context,
        IReadOnlyList<string> availableAgents,
        IReadOnlyList<LlmExecutionStep> completedSteps,
        CancellationToken cancellationToken = default);

    Task<LlmPlannerEvaluation> EvaluateStepAsync(
        AgentContext context,
        LlmExecutionStep executedStep,
        IReadOnlyList<string> availableAgents,
        IReadOnlyList<LlmExecutionStep> completedSteps,
        CancellationToken cancellationToken = default);
}

public class LlmPlannerService : ILlmPlannerService
{
    private readonly LlmService _llmService;
    private readonly IPromptService _promptService;
    private readonly ILogger<LlmPlannerService> _logger;

    public LlmPlannerService(
        LlmService llmService,
        IPromptService promptService,
        ILogger<LlmPlannerService> logger)
    {
        _llmService = llmService;
        _promptService = promptService;
        _logger = logger;
    }

    public async Task<LlmPlannerDecision> DecideNextAgentAsync(
        AgentContext context,
        IReadOnlyList<string> availableAgents,
        IReadOnlyList<LlmExecutionStep> completedSteps,
        CancellationToken cancellationToken = default)
    {
        if (availableAgents.Count == 0)
        {
            return new LlmPlannerDecision
            {
                Done = true,
                Reason = "No available agents.",
                FinalMessage = "No available agents for orchestration."
            };
        }

        var prompt = BuildDecisionPrompt(context, availableAgents, completedSteps);

        try
        {
            var systemPrompt = GetPlannerSystemPrompt();
            var response = await _llmService.GetChatCompletionAsync(systemPrompt, prompt, cancellationToken);
            var decision = ParseDecision(response);

            if (!decision.Done && string.IsNullOrWhiteSpace(decision.NextAgent))
            {
                decision.NextAgent = availableAgents[0];
                decision.Reason = string.IsNullOrWhiteSpace(decision.Reason)
                    ? "LLM did not return nextAgent; using first available agent."
                    : decision.Reason;
            }

            return decision;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LlmPlannerService] Failed to decide next agent. Falling back to first available agent.");
            return new LlmPlannerDecision
            {
                Done = false,
                NextAgent = availableAgents[0],
                Reason = "Planner fallback due to decision error."
            };
        }
    }

    public async Task<LlmPlannerEvaluation> EvaluateStepAsync(
        AgentContext context,
        LlmExecutionStep executedStep,
        IReadOnlyList<string> availableAgents,
        IReadOnlyList<LlmExecutionStep> completedSteps,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildEvaluationPrompt(context, executedStep, availableAgents, completedSteps);

        try
        {
            var systemPrompt = GetPlannerSystemPrompt();
            var response = await _llmService.GetChatCompletionAsync(systemPrompt, prompt, cancellationToken);
            return ParseEvaluation(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LlmPlannerService] Failed to evaluate step. Continuing orchestration.");
            return new LlmPlannerEvaluation
            {
                Done = false,
                Reason = "Planner fallback due to evaluation error."
            };
        }
    }

    private string GetPlannerSystemPrompt()
    {
        var prompt = _promptService.GetPrompt("OrchestratorAgent");
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "You are an orchestration planner. Return strict JSON only.";
        }

        return prompt + "\nReturn strict JSON only without markdown.";
    }

    private static string BuildDecisionPrompt(
        AgentContext context,
        IReadOnlyList<string> availableAgents,
        IReadOnlyList<LlmExecutionStep> completedSteps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Decide the next orchestration action.");
        sb.AppendLine("Return JSON with shape:");
        sb.AppendLine("{\"done\":bool,\"nextAgent\":\"string|null\",\"reason\":\"string\",\"finalMessage\":\"string|null\"}");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- done=true when the task is complete.");
        sb.AppendLine("- nextAgent must be one of availableAgents when done=false.");
        sb.AppendLine("- If uncertain, choose the safest next agent.");
        sb.AppendLine();
        sb.AppendLine("Context:");
        sb.AppendLine(JsonSerializer.Serialize(new
        {
            threadId = context.ThreadId,
            stateKeys = context.State.Keys,
            messageCount = context.Messages.Count,
            availableAgents,
            completedSteps = completedSteps.Select(s => new
            {
                s.Step,
                s.AgentName,
                s.Success,
                s.Message
            })
        }));

        return sb.ToString();
    }

    private static string BuildEvaluationPrompt(
        AgentContext context,
        LlmExecutionStep executedStep,
        IReadOnlyList<string> availableAgents,
        IReadOnlyList<LlmExecutionStep> completedSteps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Evaluate the latest orchestration step.");
        sb.AppendLine("Return JSON with shape:");
        sb.AppendLine("{\"done\":bool,\"reason\":\"string\"}");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- done=true only when the user request is fully satisfied.");
        sb.AppendLine("- done=false when additional agent steps are required.");
        sb.AppendLine();
        sb.AppendLine("Context:");
        sb.AppendLine(JsonSerializer.Serialize(new
        {
            threadId = context.ThreadId,
            stateKeys = context.State.Keys,
            availableAgents,
            executedStep,
            completedSteps = completedSteps.Select(s => new
            {
                s.Step,
                s.AgentName,
                s.Success,
                s.Message
            })
        }));

        return sb.ToString();
    }

    private static LlmPlannerDecision ParseDecision(string response)
    {
        var json = ExtractJsonObject(response);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new LlmPlannerDecision
        {
            Done = root.TryGetProperty("done", out var done) && done.GetBoolean(),
            NextAgent = root.TryGetProperty("nextAgent", out var nextAgent) && nextAgent.ValueKind == JsonValueKind.String
                ? nextAgent.GetString()
                : null,
            Reason = root.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                ? reason.GetString() ?? string.Empty
                : string.Empty,
            FinalMessage = root.TryGetProperty("finalMessage", out var finalMessage) && finalMessage.ValueKind == JsonValueKind.String
                ? finalMessage.GetString()
                : null
        };
    }

    private static LlmPlannerEvaluation ParseEvaluation(string response)
    {
        var json = ExtractJsonObject(response);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new LlmPlannerEvaluation
        {
            Done = root.TryGetProperty("done", out var done) && done.GetBoolean(),
            Reason = root.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                ? reason.GetString() ?? string.Empty
                : string.Empty
        };
    }

    private static string ExtractJsonObject(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "{}";
        }

        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return response[start..(end + 1)];
        }

        return response;
    }
}

public class LlmPlannerDecision
{
    public bool Done { get; set; }
    public string? NextAgent { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? FinalMessage { get; set; }
}

public class LlmPlannerEvaluation
{
    public bool Done { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class LlmExecutionStep
{
    public int Step { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
