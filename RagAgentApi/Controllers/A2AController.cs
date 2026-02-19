using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Services.A2A;

namespace RagAgentApi.Controllers;

/// <summary>
/// Controller for Agent-to-Agent protocol operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class A2AController : ControllerBase
{
    private readonly IA2AProtocolService _protocolService;
    private readonly ILogger<A2AController> _logger;

    public A2AController(IA2AProtocolService protocolService, ILogger<A2AController> logger)
    {
        _protocolService = protocolService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message between agents
    /// </summary>
    [HttpPost("send-message")]
    [ProducesResponseType(typeof(A2AMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendMessage([FromBody] A2AMessageRequest request)
    {
        try
        {
            _logger.LogInformation($"[A2AController] Sending message from {request.FromAgentName} to {request.ToAgentName}");

            var agents = await _protocolService.GetRegisteredAgentsAsync();
            var fromAgent = agents.FirstOrDefault(a => a.Name == request.FromAgentName);
            var toAgent = agents.FirstOrDefault(a => a.Name == request.ToAgentName);

            if (fromAgent == null || toAgent == null)
            {
                return BadRequest(new { error = "One or more agents not found" });
            }

            var message = new A2AMessage
            {
                ConversationId = request.ConversationId ?? Guid.NewGuid().ToString(),
                FromAgentId = fromAgent.Id,
                FromAgentName = request.FromAgentName,
                ToAgentId = toAgent.Id,
                ToAgentName = request.ToAgentName,
                MessageType = request.MessageType ?? "task",
                Content = request.Content,
                Payload = request.Payload
            };

            var response = await _protocolService.SendMessageAsync(message);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[A2AController] Error: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get message history for a conversation
    /// </summary>
    [HttpGet("conversations/{conversationId}/messages")]
    [ProducesResponseType(typeof(List<A2AMessage>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversationMessages(string conversationId)
    {
        try
        {
            var messages = await _protocolService.GetMessageHistoryAsync(conversationId);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[A2AController] Error: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all registered agents
    /// </summary>
    [HttpGet("agents")]
    [ProducesResponseType(typeof(List<A2AAgent>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgents()
    {
        try
        {
            var agents = await _protocolService.GetRegisteredAgentsAsync();
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[A2AController] Error: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Register a new agent
    /// </summary>
    [HttpPost("agents/register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterAgent([FromBody] A2AAgent agent)
    {
        try
        {
            if (string.IsNullOrEmpty(agent.Name))
            {
                return BadRequest(new { error = "Agent name is required" });
            }

            await _protocolService.RegisterAgentAsync(agent);
            return Ok(new { message = $"Agent {agent.Name} registered successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"[A2AController] Error: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request to send a message between agents
/// </summary>
public class A2AMessageRequest
{
    public string? ConversationId { get; set; }
    public string FromAgentName { get; set; } = string.Empty;
    public string ToAgentName { get; set; } = string.Empty;
    public string? MessageType { get; set; } = "task";
    public string Content { get; set; } = string.Empty;
    public object? Payload { get; set; }
}
