using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Agents;
using RagAgentApi.Models;

namespace RagAgentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GraphRagController : ControllerBase
{
    private readonly GraphRagAgent _agent;
    private readonly ILogger<GraphRagController> _logger;

    public GraphRagController(GraphRagAgent agent, ILogger<GraphRagController> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    public class QueryRequest
    {
        public string Query { get; set; } = string.Empty;
        public int? TopK { get; set; }
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] QueryRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req?.Query))
            return BadRequest(new { error = "Query is required" });

        var context = new AgentContext();
        context.State["query"] = req.Query;
        if (req.TopK.HasValue) context.State["top_k"] = req.TopK.Value;

        _logger.LogInformation("[GraphRagController] Running GraphRAG for query: {Query}", req.Query);

        var result = await _agent.ExecuteAsync(context, cancellationToken);

        if (!result.Success)
        {
            return StatusCode(500, new { error = result.Message, details = result.Errors });
        }

        // Return context state result if present
        if (context.State.TryGetValue("query_result_graph", out var res))
        {
            return Ok(res);
        }

        return Ok(new { message = "GraphRAG executed", result = result.Message });
    }
}
