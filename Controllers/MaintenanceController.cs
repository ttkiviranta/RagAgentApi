using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RagAgentApi.Data;

namespace RagAgentApi.Controllers;

/// <summary>
/// Maintenance controller for database operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly RagDbContext _context;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(RagDbContext context, ILogger<MaintenanceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Fix MessageCount for all conversations based on actual message counts
    /// </summary>
    [HttpPost("fix-message-counts")]
    public async Task<IActionResult> FixMessageCounts()
    {
        try
        {
            _logger.LogInformation("[Maintenance] Starting MessageCount fix");

            var conversations = await _context.Conversations.ToListAsync();
            var fixedCount = 0;

            foreach (var conversation in conversations)
            {
                var actualCount = await _context.Messages
                    .CountAsync(m => m.ConversationId == conversation.Id);

                if (conversation.MessageCount != actualCount)
                {
                    _logger.LogInformation(
                        "[Maintenance] Fixing conversation {ConversationId}: {OldCount} -> {NewCount}",
                        conversation.Id, conversation.MessageCount, actualCount);

                    conversation.MessageCount = actualCount;
                    fixedCount++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("[Maintenance] Fixed {Count} conversations", fixedCount);

            return Ok(new
            {
                message = "MessageCount fixed successfully",
                conversationsFixed = fixedCount,
                totalConversations = conversations.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Maintenance] Failed to fix MessageCounts");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get conversation statistics including message count discrepancies
    /// </summary>
    [HttpGet("conversation-stats")]
    public async Task<IActionResult> GetConversationStats()
    {
        try
        {
            var conversations = await _context.Conversations
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    StoredCount = c.MessageCount,
                    ActualCount = _context.Messages.Count(m => m.ConversationId == c.Id)
                })
                .ToListAsync();

            var discrepancies = conversations.Where(c => c.StoredCount != c.ActualCount).ToList();

            return Ok(new
            {
                totalConversations = conversations.Count,
                conversationsWithDiscrepancies = discrepancies.Count,
                discrepancies = discrepancies.Take(10) // Show first 10
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Maintenance] Failed to get conversation stats");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
