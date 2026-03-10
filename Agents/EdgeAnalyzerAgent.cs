using RagAgentApi.Models;
using System.Text.Json;

namespace RagAgentApi.Agents;

/// <summary>
/// Edge Analyzer Agent - Performs local anomaly detection at the edge
/// Analyzes sensor data patterns and triggers alerts without cloud dependency
/// </summary>
public class EdgeAnalyzerAgent : BaseRagAgent
{
    private static readonly Dictionary<string, ThresholdConfig> _thresholds = new()
    {
        { "pump-01", new ThresholdConfig(85, 5.0, 120, 180, 1600, 2000) },
        { "motor-01", new ThresholdConfig(75, 4.0, 0, 0, 3400, 3800) },
        { "compressor-01", new ThresholdConfig(90, 6.0, 160, 240, 1000, 1400) },
        { "conveyor-01", new ThresholdConfig(60, 3.0, 0, 0, 800, 1000) }
    };

    public EdgeAnalyzerAgent(ILogger<EdgeAnalyzerAgent> logger) : base(logger)
    {
    }

    public override string Name => "EdgeAnalyzerAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

        try
        {
            // Get sensor data from previous agent
            if (!context.State.TryGetValue("sensor_data", out var sensorObj) || sensorObj is not SensorDataPacket sensorData)
            {
                return AgentResult.CreateFailure("Sensor data not found in context");
            }

            var equipmentId = context.State.TryGetValue("equipment_id", out var eqId) && eqId is string id 
                ? id : "pump-01";

            _logger.LogInformation("[EdgeAnalyzerAgent] Analyzing data from {Equipment}, Health: {Health}%", 
                equipmentId, sensorData.HealthScore);

            // Perform edge-local analysis
            var analysis = AnalyzeSensorData(sensorData, equipmentId);

            // Store analysis results
            context.State["analysis_result"] = analysis;
            context.State["requires_cloud_analysis"] = analysis.Severity >= AnomalySeverity.Warning;
            context.State["anomalies_detected"] = analysis.Anomalies.Count;

            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

            // Determine next agent based on severity
            var nextAgent = analysis.Severity >= AnomalySeverity.Warning 
                ? "CloudRAGAgent" 
                : "MaintenanceSchedulerAgent";

            AddMessage(context, nextAgent, 
                $"Edge analysis complete: {analysis.Severity} severity",
                new Dictionary<string, object>
                {
                    { "severity", analysis.Severity.ToString() },
                    { "anomaly_count", analysis.Anomalies.Count },
                    { "health_score", sensorData.HealthScore },
                    { "recommendations_count", analysis.Recommendations.Count }
                });

            return AgentResult.CreateSuccess(
                $"Edge analysis complete: {analysis.Anomalies.Count} anomalies detected",
                new Dictionary<string, object>
                {
                    { "analysis", analysis },
                    { "severity", analysis.Severity.ToString() },
                    { "analysis_time_ms", stopwatch.ElapsedMilliseconds }
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
            return HandleException(ex, context.ThreadId, "edge analysis");
        }
    }

    private EdgeAnalysisResult AnalyzeSensorData(SensorDataPacket data, string equipmentId)
    {
        var result = new EdgeAnalysisResult
        {
            EquipmentId = equipmentId,
            AnalyzedAt = DateTimeOffset.UtcNow,
            HealthScore = data.HealthScore
        };

        var thresholds = _thresholds.GetValueOrDefault(equipmentId, _thresholds["pump-01"]);

        // Check temperature
        if (data.Temperature > thresholds.MaxTemperature)
        {
            var severity = data.Temperature > thresholds.MaxTemperature * 1.2 
                ? AnomalySeverity.Critical 
                : AnomalySeverity.Warning;
            
            result.Anomalies.Add(new DetectedAnomaly
            {
                Type = "HIGH_TEMPERATURE",
                Severity = severity,
                CurrentValue = data.Temperature,
                ThresholdValue = thresholds.MaxTemperature,
                Unit = "°C",
                Description = $"Temperature {data.Temperature}°C exceeds threshold {thresholds.MaxTemperature}°C"
            });

            result.Recommendations.Add(new MaintenanceRecommendation
            {
                Priority = severity == AnomalySeverity.Critical ? 1 : 2,
                Action = "Check cooling system and lubrication",
                EstimatedDowntime = TimeSpan.FromMinutes(30),
                RequiredParts = new List<string> { "Coolant", "Thermal paste" }
            });
        }

        // Check vibration
        if (data.Vibration > thresholds.MaxVibration)
        {
            var severity = data.Vibration > thresholds.MaxVibration * 1.5 
                ? AnomalySeverity.Critical 
                : AnomalySeverity.Warning;
            
            result.Anomalies.Add(new DetectedAnomaly
            {
                Type = "EXCESSIVE_VIBRATION",
                Severity = severity,
                CurrentValue = data.Vibration,
                ThresholdValue = thresholds.MaxVibration,
                Unit = "mm/s",
                Description = $"Vibration {data.Vibration} mm/s exceeds threshold {thresholds.MaxVibration} mm/s"
            });

            result.Recommendations.Add(new MaintenanceRecommendation
            {
                Priority = severity == AnomalySeverity.Critical ? 1 : 2,
                Action = "Inspect bearings and alignment",
                EstimatedDowntime = TimeSpan.FromHours(2),
                RequiredParts = new List<string> { "Bearing kit", "Alignment shims" }
            });
        }

        // Check pressure (if applicable)
        if (thresholds.MinPressure > 0 && data.Pressure < thresholds.MinPressure)
        {
            result.Anomalies.Add(new DetectedAnomaly
            {
                Type = "LOW_PRESSURE",
                Severity = AnomalySeverity.Warning,
                CurrentValue = data.Pressure,
                ThresholdValue = thresholds.MinPressure,
                Unit = "PSI",
                Description = $"Pressure {data.Pressure} PSI below minimum {thresholds.MinPressure} PSI"
            });

            result.Recommendations.Add(new MaintenanceRecommendation
            {
                Priority = 2,
                Action = "Check for leaks and seal integrity",
                EstimatedDowntime = TimeSpan.FromHours(1),
                RequiredParts = new List<string> { "O-rings", "Gasket set" }
            });
        }

        // Check RPM
        if (data.Rpm < thresholds.MinRpm || data.Rpm > thresholds.MaxRpm)
        {
            result.Anomalies.Add(new DetectedAnomaly
            {
                Type = "RPM_OUT_OF_RANGE",
                Severity = AnomalySeverity.Warning,
                CurrentValue = data.Rpm,
                ThresholdValue = data.Rpm < thresholds.MinRpm ? thresholds.MinRpm : thresholds.MaxRpm,
                Unit = "RPM",
                Description = $"RPM {data.Rpm} outside normal range ({thresholds.MinRpm}-{thresholds.MaxRpm})"
            });

            result.Recommendations.Add(new MaintenanceRecommendation
            {
                Priority = 3,
                Action = "Check motor controller and VFD settings",
                EstimatedDowntime = TimeSpan.FromMinutes(45),
                RequiredParts = new List<string>()
            });
        }

        // Set overall severity
        result.Severity = result.Anomalies.Any() 
            ? result.Anomalies.Max(a => a.Severity) 
            : AnomalySeverity.Normal;

        // Generate summary
        result.Summary = GenerateSummary(result, data);

        return result;
    }

    private string GenerateSummary(EdgeAnalysisResult result, SensorDataPacket data)
    {
        if (result.Severity == AnomalySeverity.Normal)
        {
            return $"Equipment {result.EquipmentId} operating normally. Health score: {data.HealthScore}%";
        }

        var anomalyTypes = string.Join(", ", result.Anomalies.Select(a => a.Type));
        return $"Equipment {result.EquipmentId}: {result.Severity} level alert. " +
               $"Detected: {anomalyTypes}. Health score: {data.HealthScore}%. " +
               $"Recommended actions: {result.Recommendations.Count}";
    }
}

public class EdgeAnalysisResult
{
    public string EquipmentId { get; set; } = string.Empty;
    public DateTimeOffset AnalyzedAt { get; set; }
    public int HealthScore { get; set; }
    public AnomalySeverity Severity { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<DetectedAnomaly> Anomalies { get; set; } = new();
    public List<MaintenanceRecommendation> Recommendations { get; set; } = new();
}

public class DetectedAnomaly
{
    public string Type { get; set; } = string.Empty;
    public AnomalySeverity Severity { get; set; }
    public double CurrentValue { get; set; }
    public double ThresholdValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class MaintenanceRecommendation
{
    public int Priority { get; set; }
    public string Action { get; set; } = string.Empty;
    public TimeSpan EstimatedDowntime { get; set; }
    public List<string> RequiredParts { get; set; } = new();
}

public enum AnomalySeverity
{
    Normal = 0,
    Info = 1,
    Warning = 2,
    Critical = 3
}

public record ThresholdConfig(
    double MaxTemperature, 
    double MaxVibration, 
    double MinPressure, 
    double MaxPressure, 
    int MinRpm, 
    int MaxRpm);
