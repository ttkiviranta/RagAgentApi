using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Data;
using Microsoft.EntityFrameworkCore;

namespace RagAgentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentAnalyticsController : ControllerBase
{
    private readonly RagDbContext _db;
    
    public AgentAnalyticsController(RagDbContext db)
    {
  _db = db;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetStats([FromQuery] int days = 7)
    {
        try
   {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
   
       var stats = await _db.AgentExecutions
                .Where(e => e.StartedAt > cutoffDate)
        .GroupBy(e => e.AgentName)
    .Select(g => new
      {
    AgentName = g.Key,
ExecutionCount = g.Count(),
  AvgDurationMs = g.Average(e => e.DurationMs ?? 0),
           SuccessRate = g.Count(e => e.Status == "success") / (double)g.Count()
    })
  .ToListAsync();
      
     return Ok(new { stats });
        }
        catch (Exception ex)
  {
    return StatusCode(500, new { message = ex.Message });
        }
    }
}