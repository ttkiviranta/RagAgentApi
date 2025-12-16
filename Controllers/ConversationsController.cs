using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RagAgentApi.Data;
using RagAgentApi.Models;
using RagAgentApi.Models.PostgreSQL;

namespace RagAgentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly RagDbContext _context;
    private readonly ILogger<ConversationsController> _logger;
    public ConversationsController(
        RagDbContext context,
        ILogger<ConversationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<ConversationDto>>> GetAll()
    {
        try
        {
            var conversations = await _context.Conversations
                .OrderByDescending(c => c.LastMessageAt)
                .Select(c => new ConversationDto(
                    c.Id,
                    c.Title,
                    c.CreatedAt,
                    c.LastMessageAt,
                    c.MessageCount
                ))
                .ToListAsync();

            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching conversations");
            return StatusCode(500, "Error fetching conversations");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<List<MessageDto>>> GetHistory(Guid id)
    {
        try
        {
            var messages = await _context.Messages
                .Where(m => m.ConversationId == id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            var messageDtos = messages.Select(m => new MessageDto(
                m.Id,
                m.Role,
                m.Content,
                m.CreatedAt,
                m.Sources != null
                    ? System.Text.Json.JsonSerializer.Deserialize<List<SourceDto>>(
                        m.Sources.RootElement.GetRawText(),
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        })
                    : null
            )).ToList();

            return Ok(messageDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching conversation history for {ConversationId}", id);
            return StatusCode(500, "Error fetching conversation history");
        }
    }

    [HttpPost]
    public async Task<ActionResult<CreateConversationResponse>> Create([FromBody] CreateConversationRequest? request)
    {
        var title = string.IsNullOrWhiteSpace(request?.Title)
            ? $"Conversation {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
            : request.Title;

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = title,  // ? KÄYTTÄJÄN ANTAMA TAI AUTOMAATTINEN!
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            MessageCount = 0
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        return Ok(new CreateConversationResponse(conversation.Id, conversation.Title, conversation.CreatedAt));
    }
}