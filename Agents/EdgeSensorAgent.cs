using RagAgentApi.Models;
using System.Text.Json;

namespace RagAgentApi.Agents;

/// <summary>
/// Edge Sensor Agent - Simulates IoT sensor data collection at the edge
/// Generates realistic industrial sensor readings (temperature, vibration, pressure, RPM)
/// </summary>
public class EdgeSensorAgent : BaseRagAgent
{
    private readonly Random _random = new();
    private static readonly Dictionary<string, SensorBaseline> _sensorBaselines = new()
    {
        { "pump-01", new SensorBaseline(65, 2.5, 150, 1800) },
        { "motor-01", new SensorBaseline(55, 1.8, 0, 3600) },
        { "compressor-01", new SensorBaseline(70, 3.2, 200, 1200) },
        { "conveyor-01", new SensorBaseline(45, 1.2, 0, 900) }
    };

    public EdgeSensorAgent(ILogger<EdgeSensorAgent> logger) : base(logger)
    {
    }

    public override string Name => "EdgeSensorAgent";

    public override async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogExecutionStart(context.ThreadId);

        try
        {
            // Get equipment ID from context or use default
            var equipmentId = context.State.TryGetValue("equipment_id", out var eqId) && eqId is string id 
                ? id : "pump-01";
            
            // Check if we should simulate anomaly
            var simulateAnomaly = context.State.TryGetValue("simulate_anomaly", out var anomalyObj) 
                && anomalyObj is bool anomaly && anomaly;

            var anomalyType = context.State.TryGetValue("anomaly_type", out var typeObj) && typeObj is string type
                ? type : "none";

            _logger.LogInformation("[EdgeSensorAgent] Collecting sensor data from {Equipment}, Anomaly: {Anomaly}", 
                equipmentId, simulateAnomaly ? anomalyType : "none");

            // Generate sensor readings
            var sensorData = GenerateSensorData(equipmentId, simulateAnomaly, anomalyType);

            // Store in context for next agent
            context.State["sensor_data"] = sensorData;
            context.State["equipment_id"] = equipmentId;
            context.State["collection_timestamp"] = DateTimeOffset.UtcNow;
            context.State["edge_location"] = "Factory-Floor-A";

            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, true, stopwatch.Elapsed);

            AddMessage(context, "EdgeAnalyzerAgent", 
                $"Sensor data collected from {equipmentId}",
                new Dictionary<string, object>
                {
                    { "equipment_id", equipmentId },
                    { "readings_count", 4 },
                    { "has_anomaly_simulation", simulateAnomaly }
                });

            return AgentResult.CreateSuccess(
                $"Collected sensor data from {equipmentId}",
                new Dictionary<string, object>
                {
                    { "sensor_data", sensorData },
                    { "equipment_id", equipmentId },
                    { "collection_time_ms", stopwatch.ElapsedMilliseconds }
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogExecutionComplete(context.ThreadId, false, stopwatch.Elapsed);
            return HandleException(ex, context.ThreadId, "sensor data collection");
        }
    }

    private SensorDataPacket GenerateSensorData(string equipmentId, bool simulateAnomaly, string anomalyType)
    {
        var baseline = _sensorBaselines.GetValueOrDefault(equipmentId, _sensorBaselines["pump-01"]);
        
        // Normal variation (±5%)
        var tempVariation = baseline.Temperature * 0.05;
        var vibVariation = baseline.Vibration * 0.1;
        var pressVariation = baseline.Pressure * 0.03;
        var rpmVariation = baseline.Rpm * 0.02;

        var data = new SensorDataPacket
        {
            EquipmentId = equipmentId,
            Timestamp = DateTimeOffset.UtcNow,
            Temperature = Math.Round(baseline.Temperature + (_random.NextDouble() - 0.5) * 2 * tempVariation, 1),
            Vibration = Math.Round(baseline.Vibration + (_random.NextDouble() - 0.5) * 2 * vibVariation, 2),
            Pressure = baseline.Pressure > 0 
                ? Math.Round(baseline.Pressure + (_random.NextDouble() - 0.5) * 2 * pressVariation, 1) 
                : 0,
            Rpm = (int)(baseline.Rpm + (_random.NextDouble() - 0.5) * 2 * rpmVariation),
            PowerConsumption = Math.Round(50 + _random.NextDouble() * 20, 1),
            OperatingHours = 2450 + _random.Next(0, 100)
        };

        // Apply anomaly if requested
        if (simulateAnomaly)
        {
            switch (anomalyType.ToLowerInvariant())
            {
                case "overheating":
                    data.Temperature = Math.Round(baseline.Temperature * 1.4 + _random.NextDouble() * 10, 1);
                    data.AnomalyFlags.Add("HIGH_TEMPERATURE");
                    break;
                
                case "bearing_wear":
                    data.Vibration = Math.Round(baseline.Vibration * 2.5 + _random.NextDouble() * 2, 2);
                    data.AnomalyFlags.Add("EXCESSIVE_VIBRATION");
                    break;
                
                case "pressure_drop":
                    data.Pressure = Math.Round(baseline.Pressure * 0.6, 1);
                    data.AnomalyFlags.Add("LOW_PRESSURE");
                    break;
                
                case "speed_fluctuation":
                    data.Rpm = (int)(baseline.Rpm * (0.7 + _random.NextDouble() * 0.5));
                    data.AnomalyFlags.Add("UNSTABLE_RPM");
                    break;
                
                case "critical":
                    data.Temperature = Math.Round(baseline.Temperature * 1.5, 1);
                    data.Vibration = Math.Round(baseline.Vibration * 3, 2);
                    data.AnomalyFlags.Add("HIGH_TEMPERATURE");
                    data.AnomalyFlags.Add("EXCESSIVE_VIBRATION");
                    data.AnomalyFlags.Add("CRITICAL_STATE");
                    break;
            }
        }

        // Calculate health score (0-100)
        data.HealthScore = CalculateHealthScore(data, baseline);

        return data;
    }

    private int CalculateHealthScore(SensorDataPacket data, SensorBaseline baseline)
    {
        var score = 100;

        // Temperature deviation penalty
        var tempDeviation = Math.Abs(data.Temperature - baseline.Temperature) / baseline.Temperature;
        score -= (int)(tempDeviation * 100);

        // Vibration deviation penalty
        var vibDeviation = Math.Abs(data.Vibration - baseline.Vibration) / baseline.Vibration;
        score -= (int)(vibDeviation * 50);

        // Pressure deviation penalty (if applicable)
        if (baseline.Pressure > 0)
        {
            var pressDeviation = Math.Abs(data.Pressure - baseline.Pressure) / baseline.Pressure;
            score -= (int)(pressDeviation * 30);
        }

        // RPM deviation penalty
        var rpmDeviation = Math.Abs(data.Rpm - baseline.Rpm) / (double)baseline.Rpm;
        score -= (int)(rpmDeviation * 20);

        return Math.Max(0, Math.Min(100, score));
    }
}

public class SensorDataPacket
{
    public string EquipmentId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public double Temperature { get; set; } // Celsius
    public double Vibration { get; set; } // mm/s RMS
    public double Pressure { get; set; } // PSI
    public int Rpm { get; set; }
    public double PowerConsumption { get; set; } // kW
    public int OperatingHours { get; set; }
    public int HealthScore { get; set; }
    public List<string> AnomalyFlags { get; set; } = new();
}

public record SensorBaseline(double Temperature, double Vibration, double Pressure, int Rpm);
