using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Services;
using RagAgentApi.Agents;
using RagAgentApi.Models.Requests;

namespace RagAgentApi.Controllers;

/// <summary>
/// Test controller for agent selection and factory services
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentManagementTestController : ControllerBase
{
 private readonly AgentSelectorService _selectorService;
    private readonly AgentFactory _agentFactory;
    private readonly DatabaseSeedService _seedService;
    private readonly ILogger<AgentManagementTestController> _logger;

    public AgentManagementTestController(
     AgentSelectorService selectorService,
  AgentFactory agentFactory,
        DatabaseSeedService seedService,
        ILogger<AgentManagementTestController> logger)
 {
        _selectorService = selectorService;
        _agentFactory = agentFactory;
        _seedService = seedService;
  _logger = logger;
    }

  /// <summary>
    /// Test agent selection for various URLs
    /// </summary>
 [HttpPost("test-selection")]
    public async Task<IActionResult> TestAgentSelection([FromBody] TestUrlRequest request)
    {
    try
        {
  var selectionResult = await _selectorService.TestAgentSelectionAsync(request.Url);
    
       return Ok(new
    {
   url = request.Url,
selection_result = selectionResult,
      timestamp = DateTimeOffset.UtcNow
  });
        }
 catch (Exception ex)
        {
     _logger.LogError(ex, "Agent selection test failed for URL: {Url}", request.Url);
            return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

    /// <summary>
 /// Test agent factory capabilities
 /// </summary>
    [HttpGet("factory-info")]
    public IActionResult GetFactoryInfo()
    {
        try
        {
            var factoryInfo = _agentFactory.GetFactoryInfo();
         return Ok(factoryInfo);
        }
        catch (Exception ex)
   {
 _logger.LogError(ex, "Failed to get factory info");
     return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
  }

    /// <summary>
    /// Test creating specific agents
    /// </summary>
 [HttpPost("test-agent-creation")]
    public IActionResult TestAgentCreation([FromBody] CreateAgentRequest request)
    {
     try
        {
            var canCreate = _agentFactory.CanCreateAgent(request.AgentName);
        
    if (!canCreate)
    {
 return BadRequest(new { error = $"Agent '{request.AgentName}' is not registered" });
   }

     var agent = _agentFactory.CreateAgent(request.AgentName);
     
      return Ok(new
    {
       agent_name = request.AgentName,
    agent_type = agent.GetType().Name,
        agent_display_name = agent.Name,
     creation_successful = true,
       timestamp = DateTimeOffset.UtcNow
            });
        }
 catch (Exception ex)
        {
    _logger.LogError(ex, "Agent creation test failed for: {AgentName}", request.AgentName);
return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
    }
    }

    /// <summary>
    /// Get all available agent types with their URL patterns
 /// </summary>
    [HttpGet("agent-types")]
    public async Task<IActionResult> GetAgentTypes()
   {
        try
  {
  var agentTypes = await _selectorService.GetAvailableAgentTypesAsync();
      return Ok(new
       {
       agent_types = agentTypes,
   total_count = agentTypes.Count,
          timestamp = DateTimeOffset.UtcNow
     });
        }
        catch (Exception ex)
        {
    _logger.LogError(ex, "Failed to get agent types");
     return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
      }
    }

    /// <summary>
    /// Test batch URL selection
    /// </summary>
    [HttpPost("test-batch-selection")]
    public async Task<IActionResult> TestBatchSelection()
    {
  var testUrls = new[]
        {
   "https://github.com/microsoft/semantic-kernel",
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
   "https://arxiv.org/abs/2301.00001",
          "https://yle.fi/a/74-20000123",
    "https://techcrunch.com/some-article",
     "https://example.com/some-page",
     "https://medium.com/@author/blog-post"
      };

        var results = new List<object>();

        foreach (var url in testUrls)
      {
   try
            {
             var selectionResult = await _selectorService.TestAgentSelectionAsync(url);
      results.Add(new
      {
       url = url,
       selected_agent = selectionResult.SelectedAgent.Name,
   selection_reason = selectionResult.SelectionReason,
       match_count = selectionResult.AllMatches.Count,
      top_priority = selectionResult.AllMatches.FirstOrDefault()?.Priority ?? 0
     });
      }
      catch (Exception ex)
       {
    results.Add(new
    {
   url = url,
  error = ex.Message
   });
            }
 }

        return Ok(new
        {
       test_results = results,
      total_tested = testUrls.Length,
        successful = results.Count(r => !((dynamic)r).GetType().GetProperty("error")?.GetValue(r, null)?.ToString()?.Any() ?? true),
       timestamp = DateTimeOffset.UtcNow
   });
  }

    /// <summary>
    /// Add or update URL agent mapping
    /// </summary>
    [HttpPost("mappings")]
    public async Task<IActionResult> AddMapping([FromBody] AddMappingRequest request)
    {
  try
   {
var mapping = await _selectorService.AddOrUpdateMappingAsync(
     request.AgentTypeName,
    request.Pattern,
   request.Priority,
             request.IsActive);

            return Ok(new
      {
message = "Mapping added/updated successfully",
 mapping = new
         {
    id = mapping.Id,
      agent_type = request.AgentTypeName,
    pattern = mapping.Pattern,
      priority = mapping.Priority,
               is_active = mapping.IsActive,
       created_at = mapping.CreatedAt
  },
      timestamp = DateTimeOffset.UtcNow
     });
        }
        catch (Exception ex)
     {
      _logger.LogError(ex, "Failed to add mapping");
         return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
      }
    }

    /// <summary>
    /// Get database seeding status
    /// </summary>
    [HttpGet("seeding-status")]
    public async Task<IActionResult> GetSeedingStatus()
    {
        try
        {
     var status = await _seedService.GetSeedingStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to get seeding status");
     return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }

 /// <summary>
    /// Manually trigger agent types seeding
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> TriggerSeeding()
    {
   try
  {
        await _seedService.SeedAgentTypesAsync();
       return Ok(new { message = "Seeding completed successfully", timestamp = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
   {
        _logger.LogError(ex, "Manual seeding failed");
     return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
   }
    }

    /// <summary>
    /// Test end-to-end agent pipeline creation
    /// </summary>
    [HttpPost("test-pipeline")]
    public async Task<IActionResult> TestPipelineCreation([FromBody] TestUrlRequest request)
    {
        try
  {
    // Select agent type
      var agentType = await _selectorService.SelectAgentTypeAsync(request.Url);
            
            // Create pipeline
   var pipeline = _agentFactory.CreatePipeline(agentType);

    return Ok(new
       {
 url = request.Url,
      selected_agent_type = agentType.Name,
  pipeline = pipeline.Select((agent, index) => new
  {
          step = index + 1,
         agent_name = agent.Name,
agent_type = agent.GetType().Name
    }).ToList(),
 pipeline_length = pipeline.Count,
      timestamp = DateTimeOffset.UtcNow
         });
      }
        catch (Exception ex)
        {
      _logger.LogError(ex, "Pipeline creation test failed for URL: {Url}", request.Url);
   return StatusCode(500, new { error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }
}