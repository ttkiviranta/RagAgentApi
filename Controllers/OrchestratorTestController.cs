using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Agents;
using RagAgentApi.Services;
using RagAgentApi.Models;
using RagAgentApi.Models.Requests;
using RagAgentApi.Data;
using Microsoft.EntityFrameworkCore;

namespace RagAgentApi.Controllers;

/// <summary>
/// Test controller for enhanced orchestrator functionality
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrchestratorTestController : ControllerBase
{
    private readonly OrchestratorAgent _orchestratorAgent;
    private readonly AgentSelectorService _agentSelectorService;
    private readonly AgentFactory _agentFactory;
    private readonly AgentOrchestrationService _orchestrationService;
    private readonly RagDbContext _context;
    private readonly ILogger<OrchestratorTestController> _logger;

    public OrchestratorTestController(
        OrchestratorAgent orchestratorAgent,
        AgentSelectorService agentSelectorService,
   AgentFactory agentFactory,
        AgentOrchestrationService orchestrationService,
     RagDbContext context,
        ILogger<OrchestratorTestController> logger)
    {
        _orchestratorAgent = orchestratorAgent;
        _agentSelectorService = agentSelectorService;
        _agentFactory = agentFactory;
        _orchestrationService = orchestrationService;
        _context = context;
  _logger = logger;
    }

    /// <summary>
    /// Test enhanced orchestrator with various URL types
 /// </summary>
    [HttpPost("test-enhanced-orchestration")]
    public async Task<IActionResult> TestEnhancedOrchestration([FromBody] TestUrlRequest request)
    {
        try
        {
  _logger.LogInformation("Testing enhanced orchestration for URL: {Url}", request.Url);

    // Create test context
            var context = _orchestrationService.CreateContext();
       context.State["url"] = request.Url;
        context.State["chunk_size"] = 1000;
     context.State["chunk_overlap"] = 200;

  // Execute enhanced orchestrator
      var result = await _orchestratorAgent.ExecuteAsync(context);

            // Get execution history for this thread
            var executions = await GetExecutionHistoryAsync(Guid.Parse(context.ThreadId));

            var response = new
   {
    url = request.Url,
     thread_id = context.ThreadId,
           orchestration_result = new
                {
       success = result.Success,
       message = result.Message,
  agent_type = result.Data?.GetValueOrDefault("agent_type"),
         pipeline_agents = result.Data?.GetValueOrDefault("pipeline_agents"),
          execution_time_ms = result.Data?.GetValueOrDefault("total_execution_time_ms"),
       steps_completed = result.Data?.GetValueOrDefault("successful_steps"),
        total_steps = result.Data?.GetValueOrDefault("pipeline_length")
       },
                execution_history = executions,
        context_state = context.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            timestamp = DateTimeOffset.UtcNow
       };

         return Ok(response);
        }
        catch (Exception ex)
        {
      _logger.LogError(ex, "Enhanced orchestration test failed for URL: {Url}", request.Url);
       return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
    /// Test batch orchestration with multiple URL types
 /// </summary>
    [HttpPost("test-batch-orchestration")]
    public async Task<IActionResult> TestBatchOrchestration()
    {
        var testUrls = new[]
        {
       "https://github.com/microsoft/semantic-kernel",
     "https://www.youtube.com/watch?v=dQw4w9WgXcQ", 
         "https://arxiv.org/abs/2301.00001",
  "https://yle.fi/a/74-20000123",
            "https://example.com/test-page"
      };

        var results = new List<object>();

      foreach (var url in testUrls)
      {
        try
            {
    // Predict which agent will be selected
                var selectedAgentType = await _agentSelectorService.SelectAgentTypeAsync(url);
       var pipeline = _agentFactory.CreatePipeline(selectedAgentType);

                // Create test context (lightweight test, doesn't execute full pipeline)
                var context = _orchestrationService.CreateContext();
              context.State["url"] = url;
          context.State["chunk_size"] = 500;
            context.State["chunk_overlap"] = 100;

       results.Add(new
        {
 url = url,
            thread_id = context.ThreadId,
      predicted_agent_type = selectedAgentType.Name,
      predicted_pipeline = pipeline.Select(a => a.Name).ToList(),
     pipeline_length = pipeline.Count,
                agent_description = selectedAgentType.Description,
         test_mode = "prediction_only"
           });
    }
       catch (Exception ex)
       {
     results.Add(new
 {
        url = url,
        error = ex.Message,
  test_mode = "failed"
       });
       }
  }

   return Ok(new
        {
       test_summary = new
            {
           total_urls = testUrls.Length,
  successful_predictions = results.Count(r => !((dynamic)r).GetType().GetProperty("error")?.GetValue(r, null)?.ToString()?.Any() ?? true),
     failed_predictions = results.Count(r => ((dynamic)r).GetType().GetProperty("error")?.GetValue(r, null)?.ToString()?.Any() ?? false)
    },
 results = results,
   timestamp = DateTimeOffset.UtcNow
     });
    }

    /// <summary>
    /// Test specific agent pipeline creation
    /// </summary>
    [HttpPost("test-pipeline-creation")]
    public async Task<IActionResult> TestPipelineCreation([FromBody] TestAgentTypeRequest request)
    {
        try
   {
    var agentType = await _context.AgentTypes
         .FirstOrDefaultAsync(at => at.Name == request.AgentTypeName && at.IsActive);

          if (agentType == null)
{
 return NotFound(new { error = $"Agent type '{request.AgentTypeName}' not found" });
 }

   var pipeline = _agentFactory.CreatePipeline(agentType);

            var response = new
            {
      agent_type = new
         {
     name = agentType.Name,
    description = agentType.Description,
    is_active = agentType.IsActive,
             created_at = agentType.CreatedAt
          },
             pipeline = pipeline.Select((agent, index) => new
    {
  step = index + 1,
 agent_name = agent.Name,
   agent_type = agent.GetType().Name,
        agent_namespace = agent.GetType().Namespace
           }).ToList(),
pipeline_summary = new
   {
    total_steps = pipeline.Count,
     agent_types = pipeline.Select(a => a.GetType().Name).Distinct().ToList(),
   pipeline_string = string.Join(" -> ", pipeline.Select(a => a.Name))
      },
      timestamp = DateTimeOffset.UtcNow
  };

   return Ok(response);
        }
        catch (Exception ex)
        {
         _logger.LogError(ex, "Pipeline creation test failed for agent type: {AgentTypeName}", request.AgentTypeName);
     return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
    /// Get execution statistics and analytics
    /// </summary>
 [HttpGet("execution-analytics")]
    public async Task<IActionResult> GetExecutionAnalytics([FromQuery] int? lastHours = 24)
{
        try
   {
      var since = DateTime.UtcNow.AddHours(-(lastHours ?? 24));

            var analytics = new
  {
                time_period = new
{
                    since = since,
        hours = lastHours ?? 24
  },
     execution_stats = new
             {
       total_executions = await _context.AgentExecutions
            .CountAsync(e => e.StartedAt >= since),
     successful_executions = await _context.AgentExecutions
         .CountAsync(e => e.StartedAt >= since && e.Status == "success"),
                 failed_executions = await _context.AgentExecutions
            .CountAsync(e => e.StartedAt >= since && e.Status == "failed"),
            running_executions = await _context.AgentExecutions
         .CountAsync(e => e.StartedAt >= since && e.Status == "running")
       },
     agent_usage = await _context.AgentExecutions
      .Where(e => e.StartedAt >= since && e.ParentExecutionId == null) // Only pipeline parents
          .GroupBy(e => e.AgentName)
            .Select(g => new
              {
         agent_type = g.Key,
   usage_count = g.Count(),
           success_rate = g.Count(e => e.Status == "success") / (double)g.Count(),
             avg_duration_ms = g.Where(e => e.DurationMs.HasValue).Average(e => e.DurationMs.Value)
       })
                    .OrderByDescending(x => x.usage_count)
  .ToListAsync(),
        performance_metrics = new
        {
  avg_pipeline_duration_ms = await _context.AgentExecutions
     .Where(e => e.StartedAt >= since && e.ParentExecutionId == null && e.DurationMs.HasValue)
 .AverageAsync(e => e.DurationMs.Value),
      fastest_execution_ms = await _context.AgentExecutions
          .Where(e => e.StartedAt >= since && e.ParentExecutionId == null && e.DurationMs.HasValue)
   .MinAsync(e => e.DurationMs.Value),
      slowest_execution_ms = await _context.AgentExecutions
      .Where(e => e.StartedAt >= since && e.ParentExecutionId == null && e.DurationMs.HasValue)
  .MaxAsync(e => e.DurationMs.Value)
     },
        recent_executions = await _context.AgentExecutions
     .Where(e => e.StartedAt >= since && e.ParentExecutionId == null)
   .OrderByDescending(e => e.StartedAt)
    .Take(10)
    .Select(e => new
                {
       id = e.Id,
        thread_id = e.ThreadId,
  agent_name = e.AgentName,
    status = e.Status,
       started_at = e.StartedAt,
        duration_ms = e.DurationMs,
            error_message = e.ErrorMessage
         })
 .ToListAsync(),
     timestamp = DateTimeOffset.UtcNow
            };

            return Ok(analytics);
        }
      catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get execution analytics");
            return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
    /// Get detailed execution history for a specific thread
    /// </summary>
    [HttpGet("thread/{threadId}/execution-history")]
    public async Task<IActionResult> GetThreadExecutionHistory(Guid threadId)
    {
        try
        {
            var executions = await GetExecutionHistoryAsync(threadId);

            if (!executions.Any())
     {
    return NotFound(new { error = $"No execution history found for thread {threadId}" });
      }

      return Ok(new
            {
                thread_id = threadId,
       execution_history = executions,
           summary = new
           {
      total_executions = executions.Count,
          pipeline_executions = executions.Count(e => e.parent_execution_id == null),
       step_executions = executions.Count(e => e.parent_execution_id != null),
        successful_executions = executions.Count(e => e.status == "success"),
        failed_executions = executions.Count(e => e.status == "failed"),
    total_duration_ms = executions.Where(e => e.duration_ms.HasValue).Sum(e => e.duration_ms.Value)
},
         timestamp = DateTimeOffset.UtcNow
        });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get thread execution history for {ThreadId}", threadId);
  return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    private async Task<List<dynamic>> GetExecutionHistoryAsync(Guid threadId)
    {
return await _context.AgentExecutions
            .Where(e => e.ThreadId == threadId)
   .OrderBy(e => e.StartedAt)
  .Select(e => new
       {
                id = e.Id,
           agent_name = e.AgentName,
     parent_execution_id = e.ParentExecutionId,
          started_at = e.StartedAt,
     completed_at = e.CompletedAt,
         duration_ms = e.DurationMs,
                status = e.Status,
     error_message = e.ErrorMessage
     })
            .Cast<dynamic>()
            .ToListAsync();
    }
}