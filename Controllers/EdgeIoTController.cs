using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Agents;
using RagAgentApi.Models;
using RagAgentApi.Services;
using System.Text.Json;

namespace RagAgentApi.Controllers;

/// <summary>
/// Controller for Edge IoT POC Demo - Industrial Predictive Maintenance
/// Demonstrates edge-cloud hybrid AI agent architecture
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EdgeIoTController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentOrchestrationService _orchestrationService;
    private readonly ILogger<EdgeIoTController> _logger;

    public EdgeIoTController(
        IServiceProvider serviceProvider,
        AgentOrchestrationService orchestrationService,
        ILogger<EdgeIoTController> logger)
    {
        _serviceProvider = serviceProvider;
        _orchestrationService = orchestrationService;
        _logger = logger;
    }

    /// <summary>
    /// Get list of available equipment for monitoring
    /// </summary>
    [HttpGet("equipment")]
    public IActionResult GetEquipment()
    {
        var equipment = new[]
        {
            new EquipmentInfo { Id = "pump-01", Name = "Main Coolant Pump", Type = "Centrifugal Pump", Location = "Building A, Floor 1", Status = "Online" },
            new EquipmentInfo { Id = "motor-01", Name = "Production Line Motor", Type = "AC Induction Motor", Location = "Building A, Floor 2", Status = "Online" },
            new EquipmentInfo { Id = "compressor-01", Name = "Air Compressor Unit", Type = "Rotary Screw", Location = "Building B, Utility", Status = "Online" },
            new EquipmentInfo { Id = "conveyor-01", Name = "Assembly Conveyor", Type = "Belt Conveyor", Location = "Building A, Floor 1", Status = "Online" }
        };

        return Ok(new { equipment, timestamp = DateTimeOffset.UtcNow });
    }

    /// <summary>
    /// Collect sensor data from specified equipment (simulated)
    /// </summary>
    [HttpPost("collect/{equipmentId}")]
    public async Task<IActionResult> CollectSensorData(
        string equipmentId, 
        [FromQuery] bool simulateAnomaly = false,
        [FromQuery] string anomalyType = "none",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[EdgeIoT] Collecting data from {Equipment}, Anomaly: {Anomaly}", 
                equipmentId, simulateAnomaly ? anomalyType : "none");

            var sensorAgent = _serviceProvider.GetRequiredService<EdgeSensorAgent>();
            var context = _orchestrationService.CreateContext();
            
            context.State["equipment_id"] = equipmentId;
            context.State["simulate_anomaly"] = simulateAnomaly;
            context.State["anomaly_type"] = anomalyType;

            var result = await sensorAgent.ExecuteAsync(context, cancellationToken);

            if (!result.Success)
            {
                return StatusCode(500, new { error = result.Message });
            }

            return Ok(new
            {
                success = true,
                threadId = context.ThreadId,
                sensorData = context.State.GetValueOrDefault("sensor_data"),
                collectionTimestamp = context.State.GetValueOrDefault("collection_timestamp"),
                edgeLocation = context.State.GetValueOrDefault("edge_location")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EdgeIoT] Error collecting sensor data");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Run full edge analysis pipeline on equipment
    /// </summary>
    [HttpPost("analyze/{equipmentId}")]
    public async Task<IActionResult> AnalyzeEquipment(
        string equipmentId,
        [FromQuery] bool simulateAnomaly = false,
        [FromQuery] string anomalyType = "none",
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("[EdgeIoT] Running analysis pipeline for {Equipment}", equipmentId);

            var context = _orchestrationService.CreateContext();
            context.State["equipment_id"] = equipmentId;
            context.State["simulate_anomaly"] = simulateAnomaly;
            context.State["anomaly_type"] = anomalyType;

            var pipelineSteps = new List<EdgePipelineStep>();

            // Step 1: Sensor Collection
            var sensorAgent = _serviceProvider.GetRequiredService<EdgeSensorAgent>();
            var sensorStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var sensorResult = await sensorAgent.ExecuteAsync(context, cancellationToken);
            sensorStopwatch.Stop();

            pipelineSteps.Add(new EdgePipelineStep
            {
                StepNumber = 1,
                AgentName = "EdgeSensorAgent",
                AgentType = "Edge",
                Action = "Data Collection",
                Success = sensorResult.Success,
                ExecutionTimeMs = sensorStopwatch.ElapsedMilliseconds,
                Output = sensorResult.Data
            });

            if (!sensorResult.Success)
            {
                return Ok(CreatePipelineResponse(context, pipelineSteps, stopwatch, false, sensorResult.Message));
            }

            // Step 2: Edge Analysis
            var analyzerAgent = _serviceProvider.GetRequiredService<EdgeAnalyzerAgent>();
            var analyzerStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var analyzerResult = await analyzerAgent.ExecuteAsync(context, cancellationToken);
            analyzerStopwatch.Stop();

            pipelineSteps.Add(new EdgePipelineStep
            {
                StepNumber = 2,
                AgentName = "EdgeAnalyzerAgent",
                AgentType = "Edge",
                Action = "Anomaly Detection",
                Success = analyzerResult.Success,
                ExecutionTimeMs = analyzerStopwatch.ElapsedMilliseconds,
                Output = analyzerResult.Data
            });

            // Step 3: Cloud RAG Query (if anomaly detected)
            var requiresCloud = context.State.TryGetValue("requires_cloud_analysis", out var cloudObj) 
                && cloudObj is bool needsCloud && needsCloud;

            if (requiresCloud && analyzerResult.Success)
            {
                var ragStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Simulate cloud RAG query for maintenance documentation
                var analysisResult = context.State.GetValueOrDefault("analysis_result") as EdgeAnalysisResult;
                var ragQuery = GenerateRAGQuery(analysisResult);
                
                // In real implementation, this would query the RAG system
                var ragResponse = await SimulateRAGQuery(ragQuery, analysisResult);
                ragStopwatch.Stop();

                context.State["rag_response"] = ragResponse;

                pipelineSteps.Add(new EdgePipelineStep
                {
                    StepNumber = 3,
                    AgentName = "CloudRAGAgent",
                    AgentType = "Cloud",
                    Action = "Documentation Query",
                    Success = true,
                    ExecutionTimeMs = ragStopwatch.ElapsedMilliseconds,
                    Output = new Dictionary<string, object>
                    {
                        { "query", ragQuery },
                        { "documents_found", ragResponse.RelevantDocs.Count },
                        { "maintenance_procedures", ragResponse.Procedures.Count }
                    }
                });

                // Step 4: Generate Maintenance Work Order
                var workOrderStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var workOrder = GenerateWorkOrder(equipmentId, analysisResult, ragResponse);
                workOrderStopwatch.Stop();

                context.State["work_order"] = workOrder;

                pipelineSteps.Add(new EdgePipelineStep
                {
                    StepNumber = 4,
                    AgentName = "MaintenanceAgent",
                    AgentType = "Cloud",
                    Action = "Work Order Generation",
                    Success = true,
                    ExecutionTimeMs = workOrderStopwatch.ElapsedMilliseconds,
                    Output = new Dictionary<string, object>
                    {
                        { "work_order_id", workOrder.Id },
                        { "priority", workOrder.Priority },
                        { "estimated_duration", workOrder.EstimatedDuration.TotalMinutes + " minutes" }
                    }
                });
            }

            stopwatch.Stop();
            return Ok(CreatePipelineResponse(context, pipelineSteps, stopwatch, true, "Analysis complete"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EdgeIoT] Error in analysis pipeline");
            stopwatch.Stop();
            return StatusCode(500, new { error = ex.Message, executionTimeMs = stopwatch.ElapsedMilliseconds });
        }
    }

    /// <summary>
    /// Get available anomaly types for simulation
    /// </summary>
    [HttpGet("anomaly-types")]
    public IActionResult GetAnomalyTypes()
    {
        var types = new[]
        {
            new { id = "none", name = "Normal Operation", description = "No anomaly - equipment operating within normal parameters" },
            new { id = "overheating", name = "Overheating", description = "Temperature exceeds safe operating limits" },
            new { id = "bearing_wear", name = "Bearing Wear", description = "Excessive vibration indicating bearing degradation" },
            new { id = "pressure_drop", name = "Pressure Drop", description = "System pressure below minimum threshold" },
            new { id = "speed_fluctuation", name = "Speed Fluctuation", description = "RPM unstable or outside normal range" },
            new { id = "critical", name = "Critical Failure", description = "Multiple anomalies indicating imminent failure" }
        };

        return Ok(new { anomalyTypes = types });
    }

    private string GenerateRAGQuery(EdgeAnalysisResult? analysis)
    {
        if (analysis == null || !analysis.Anomalies.Any())
            return "Standard maintenance procedures";

        var anomalyTypes = string.Join(" ", analysis.Anomalies.Select(a => a.Type));
        return $"Maintenance procedures for {analysis.EquipmentId} with {anomalyTypes} issues";
    }

    private async Task<RAGQueryResponse> SimulateRAGQuery(string query, EdgeAnalysisResult? analysis)
    {
        // Simulate network latency
        await Task.Delay(100);

        var response = new RAGQueryResponse
        {
            Query = query,
            RelevantDocs = new List<string>
            {
                $"Maintenance Manual - {analysis?.EquipmentId ?? "Equipment"} Section 4.2",
                "Troubleshooting Guide - Industrial Pumps",
                "Safety Procedures - High Temperature Operations"
            },
            Procedures = new List<string>
            {
                "1. Shut down equipment following safety protocol SOP-001",
                "2. Allow cooling period of 30 minutes",
                "3. Inspect affected components as per checklist CL-042",
                "4. Replace worn parts using approved spares",
                "5. Perform test run and verify parameters"
            }
        };

        return response;
    }

    private WorkOrder GenerateWorkOrder(string equipmentId, EdgeAnalysisResult? analysis, RAGQueryResponse ragResponse)
    {
        return new WorkOrder
        {
            Id = $"WO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            EquipmentId = equipmentId,
            Priority = analysis?.Severity == AnomalySeverity.Critical ? "URGENT" : "HIGH",
            Description = analysis?.Summary ?? "Scheduled maintenance",
            CreatedAt = DateTimeOffset.UtcNow,
            EstimatedDuration = analysis?.Recommendations.Sum(r => r.EstimatedDowntime.TotalMinutes) 
                is double minutes ? TimeSpan.FromMinutes(minutes) : TimeSpan.FromHours(1),
            RequiredParts = analysis?.Recommendations.SelectMany(r => r.RequiredParts).Distinct().ToList() ?? new(),
            Procedures = ragResponse.Procedures,
            AssignedTo = "Maintenance Team A"
        };
    }

    private object CreatePipelineResponse(
        AgentContext context, 
        List<EdgePipelineStep> steps, 
        System.Diagnostics.Stopwatch stopwatch, 
        bool success, 
        string message)
    {
        return new
        {
            success,
            message,
            threadId = context.ThreadId,
            totalExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            edgeProcessingTimeMs = steps.Where(s => s.AgentType == "Edge").Sum(s => s.ExecutionTimeMs),
            cloudProcessingTimeMs = steps.Where(s => s.AgentType == "Cloud").Sum(s => s.ExecutionTimeMs),
            pipelineSteps = steps,
            sensorData = context.State.GetValueOrDefault("sensor_data"),
            analysisResult = context.State.GetValueOrDefault("analysis_result"),
            workOrder = context.State.GetValueOrDefault("work_order"),
            summary = new
            {
                equipmentId = context.State.GetValueOrDefault("equipment_id"),
                anomaliesDetected = context.State.GetValueOrDefault("anomalies_detected"),
                requiresCloudAnalysis = context.State.GetValueOrDefault("requires_cloud_analysis"),
                timestamp = DateTimeOffset.UtcNow
            }
        };
    }
}

public class EquipmentInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class EdgePipelineStep
{
    public int StepNumber { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty; // "Edge" or "Cloud"
    public string Action { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long ExecutionTimeMs { get; set; }
    public Dictionary<string, object>? Output { get; set; }
}

public class RAGQueryResponse
{
    public string Query { get; set; } = string.Empty;
    public List<string> RelevantDocs { get; set; } = new();
    public List<string> Procedures { get; set; } = new();
}

public class WorkOrder
{
    public string Id { get; set; } = string.Empty;
    public string EquipmentId { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public List<string> RequiredParts { get; set; } = new();
    public List<string> Procedures { get; set; } = new();
    public string AssignedTo { get; set; } = string.Empty;
}
