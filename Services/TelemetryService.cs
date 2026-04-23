using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace RagAgentApi.Services;

public interface ITelemetryService
{
    void TrackEvent(string name, IDictionary<string, string>? properties = null);
    void TrackMetric(string name, double value, IDictionary<string, string>? properties = null);
}

/// <summary>
/// Lightweight wrapper around Application Insights TelemetryClient.
/// Used by agents and guardrails to report LLM call telemetry and violations.
/// </summary>
public class TelemetryService : ITelemetryService
{
    private readonly TelemetryClient _client;
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(TelemetryClient client, ILogger<TelemetryService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public void TrackEvent(string name, IDictionary<string, string>? properties = null)
    {
        try
        {
            _client.TrackEvent(name, properties);
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug(ex, "Telemetry TrackEvent failed: {Event}", name);
        }
    }

    public void TrackMetric(string name, double value, IDictionary<string, string>? properties = null)
    {
        try
        {
            _client.GetMetric(name).TrackValue(value);
            // Also emit lightweight event for dimensions
            _client.TrackEvent(name + "_metric", properties);
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug(ex, "Telemetry TrackMetric failed: {Metric}", name);
        }
    }
}
