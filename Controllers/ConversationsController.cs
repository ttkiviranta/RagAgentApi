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
            _logger.LogInformation("[ConversationsController] GetAll called");
            
            // Test database connection
            var canConnect = await _context.Database.CanConnectAsync();
            _logger.LogInformation("[ConversationsController] Database connection: {CanConnect}", canConnect);
            
            if (!canConnect)
            {
                _logger.LogError("[ConversationsController] Cannot connect to database");
                return StatusCode(500, "Cannot connect to database");
            }

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

            _logger.LogInformation("[ConversationsController] Found {Count} conversations", conversations.Count);
            
            if (conversations.Any())
            {
                _logger.LogDebug("[ConversationsController] First conversation: {Title}, Messages: {Count}", 
                    conversations[0].Title, conversations[0].MessageCount);
            }

            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationsController] Error fetching conversations");
            return StatusCode(500, new { error = "Error fetching conversations", details = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<List<MessageDto>>> GetHistory(Guid id)
    {
        try
        {
            _logger.LogInformation("[ConversationsController] GetHistory called for {ConversationId}", id);
            
            var messages = await _context.Messages
                .Where(m => m.ConversationId == id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("[ConversationsController] Found {Count} messages for conversation {ConversationId}", 
                messages.Count, id);

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
            _logger.LogError(ex, "[ConversationsController] Error fetching conversation history for {ConversationId}", id);
            return StatusCode(500, new { error = "Error fetching conversation history", details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<CreateConversationResponse>> Create([FromBody] CreateConversationRequest? request)
    {
        try
        {
            _logger.LogInformation("[ConversationsController] Create called with title: {Title}", request?.Title);
            
            var title = string.IsNullOrWhiteSpace(request?.Title)
                ? $"Conversation {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
                : request.Title;

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Title = title,
                Status = "active",
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                MessageCount = 0
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("[ConversationsController] Created conversation {ConversationId} with title: {Title}", 
                conversation.Id, conversation.Title);

            return Ok(new CreateConversationResponse(conversation.Id, conversation.Title!, conversation.CreatedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationsController] Error creating conversation");
            return StatusCode(500, new { error = "Error creating conversation", details = ex.Message });
        }
    }
}