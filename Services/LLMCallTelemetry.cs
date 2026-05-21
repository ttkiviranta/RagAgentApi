namespace RagAgentApi.Services;

/// <summary>
/// Encapsulates telemetry data for a single LLM call.
/// Used to standardize and track all LLM invocations across agents.
/// </summary>
public class LLMCallTelemetry
{
    public string AgentName { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> AdditionalProperties { get; set; } = new();

    public LLMCallTelemetry WithAgent(string agentName)
    {
        AgentName = agentName;
        return this;
    }

    public LLMCallTelemetry WithPromptVersion(string version)
    {
        PromptVersion = version;
        return this;
    }

    public LLMCallTelemetry WithModelVersion(string version)
    {
        ModelVersion = version;
        return this;
    }

    public LLMCallTelemetry WithLatency(long ms)
    {
        LatencyMs = ms;
        return this;
    }

    public LLMCallTelemetry WithTokens(int input, int output)
    {
        InputTokens = input;
        OutputTokens = output;
        return this;
    }

    public LLMCallTelemetry WithSuccess(bool success, string? error = null)
    {
        Success = success;
        ErrorMessage = error;
        return this;
    }

    public LLMCallTelemetry AddProperty(string key, string value)
    {
        AdditionalProperties[key] = value;
        return this;
    }

    public void Track(ITelemetryService telemetry)
    {
        telemetry.TrackLLMCall(AgentName, PromptVersion, ModelVersion, LatencyMs, InputTokens, OutputTokens, Success, ErrorMessage);
    }
}
