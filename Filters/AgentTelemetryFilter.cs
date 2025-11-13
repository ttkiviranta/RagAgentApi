using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Agents;
using System.Diagnostics;

namespace RagAgentApi.Filters;

/// <summary>
/// Filter for tracking agent execution telemetry in Application Insights
/// </summary>
public class AgentTelemetryFilter : IActionFilter
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<AgentTelemetryFilter> _logger;
    private const string StopwatchKey = "AgentExecutionStopwatch";

    public AgentTelemetryFilter(TelemetryClient telemetryClient, ILogger<AgentTelemetryFilter> logger)
    {
      _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
    var stopwatch = Stopwatch.StartNew();
        context.HttpContext.Items[StopwatchKey] = stopwatch;

  var actionName = context.ActionDescriptor.DisplayName ?? "Unknown Action";
      var threadId = ExtractThreadId(context);

  _logger.LogInformation("[Telemetry] Action {ActionName} starting on thread {ThreadId}", actionName, threadId);

        // Track custom event
        _telemetryClient.TrackEvent("ActionStarted", new Dictionary<string, string>
        {
         { "ActionName", actionName },
    { "ThreadId", threadId },
            { "Timestamp", DateTimeOffset.UtcNow.ToString("O") }
   });
    }

    public void OnActionExecuted(ActionExecutedContext context)
  {
  if (context.HttpContext.Items.TryGetValue(StopwatchKey, out var stopwatchObj) && 
   stopwatchObj is Stopwatch stopwatch)
        {
        stopwatch.Stop();
  var duration = stopwatch.Elapsed;

     var actionName = context.ActionDescriptor.DisplayName ?? "Unknown Action";
            var threadId = ExtractThreadId(context);
            var success = context.Exception == null;

      _logger.LogInformation("[Telemetry] Action {ActionName} completed in {Duration}ms - Success: {Success}", 
  actionName, duration.TotalMilliseconds, success);

        // Track execution time metric
            _telemetryClient.TrackMetric("ActionExecutionTime", duration.TotalMilliseconds, new Dictionary<string, string>
    {
   { "ActionName", actionName },
      { "Success", success.ToString() }
   });

      // Track completion event
       _telemetryClient.TrackEvent("ActionCompleted", new Dictionary<string, string>
            {
    { "ActionName", actionName },
       { "ThreadId", threadId },
    { "Success", success.ToString() },
                { "Duration", duration.TotalMilliseconds.ToString("F2") }
            });

            // Track any exceptions
            if (context.Exception != null)
    {
              _telemetryClient.TrackException(context.Exception, new Dictionary<string, string>
{
           { "ActionName", actionName },
   { "ThreadId", threadId }
         });
   }

  // Extract specific metrics based on action result
  TrackSpecificMetrics(context, actionName, threadId);
        }
    }

    private string ExtractThreadId(ActionContext context)
    {
        // Try to extract thread ID from route data
        if (context.RouteData.Values.TryGetValue("threadId", out var threadId))
    {
   return threadId?.ToString() ?? "Unknown";
    }

    // Try to extract from request body for POST requests
        if (context.HttpContext.Request.Method == "POST")
        {
         // For ingest operations, we'll generate a new thread ID
            return "NewThread";
        }

     return "Unknown";
    }

    private void TrackSpecificMetrics(ActionExecutedContext context, string actionName, string threadId)
    {
        if (context.Result is not Microsoft.AspNetCore.Mvc.ObjectResult objectResult)
            return;

 var resultValue = objectResult.Value;
        if (resultValue == null)
 return;

        // Use reflection to extract metrics from anonymous objects or known types
        var resultType = resultValue.GetType();

        // Track chunks processed (for ingest operations)
        TrackPropertyMetric(resultValue, "chunks_processed", "ChunksProcessed", actionName);
        
        // Track source count (for query operations)
        TrackPropertyMetric(resultValue, "source_count", "SourceCount", actionName);
        
        // Track execution time
        TrackPropertyMetric(resultValue, "execution_time_ms", "ExecutionTime", actionName);
        TrackPropertyMetric(resultValue, "processing_time_ms", "ProcessingTime", actionName);
TrackPropertyMetric(resultValue, "storage_time_ms", "StorageTime", actionName);

        // Track document metrics
    TrackPropertyMetric(resultValue, "documents_stored", "DocumentsStored", actionName);
    TrackPropertyMetric(resultValue, "embedding_count", "EmbeddingCount", actionName);
        TrackPropertyMetric(resultValue, "content_length", "ContentLength", actionName);
    }

    private void TrackPropertyMetric(object obj, string propertyName, string metricName, string actionName)
    {
        try
        {
  var property = obj.GetType().GetProperty(propertyName);
         if (property?.GetValue(obj) is int intValue)
      {
  _telemetryClient.TrackMetric(metricName, intValue, new Dictionary<string, string>
      {
       { "ActionName", actionName }
           });
   }
            else if (property?.GetValue(obj) is long longValue)
            {
  _telemetryClient.TrackMetric(metricName, longValue, new Dictionary<string, string>
   {
         { "ActionName", actionName }
       });
 }
   else if (property?.GetValue(obj) is double doubleValue)
{
      _telemetryClient.TrackMetric(metricName, doubleValue, new Dictionary<string, string>
            {
          { "ActionName", actionName }
     });
       }
        }
  catch (Exception ex)
        {
  _logger.LogDebug("Failed to extract metric {MetricName} from property {PropertyName}: {Error}", 
      metricName, propertyName, ex.Message);
        }
    }
}