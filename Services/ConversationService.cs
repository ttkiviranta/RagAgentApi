using RagAgentApi.Data;
using RagAgentApi.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using System.Text.Json;

namespace RagAgentApi.Services;

/// <summary>
/// Service for managing conversations and messages
/// </summary>
public class ConversationService
{
    private readonly RagDbContext _context;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(RagDbContext context, ILogger<ConversationService> logger)
    {
        _context = context;
        _logger = logger;
}

    /// <summary>
    /// Create a new conversation
    /// </summary>
    public async Task<Conversation> CreateConversationAsync(
      string? userId = null,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
    try
        {
            var conversation = new Conversation
  {
          UserId = userId,
      Title = title,
    CreatedAt = DateTime.UtcNow,
       LastMessageAt = DateTime.UtcNow,
 Status = "active"
          };

    _context.Conversations.Add(conversation);
    await _context.SaveChangesAsync(cancellationToken);

 _logger.LogInformation("[ConversationService] Created conversation {ConversationId}", conversation.Id);
    return conversation;
        }
        catch (Exception ex)
    {
     _logger.LogError(ex, "[ConversationService] Failed to create conversation");
            throw;
        }
    }

    /// <summary>
    /// Add a message to a conversation
    /// </summary>
    public async Task<Message> AddMessageAsync(
     Guid conversationId,
        string role,
  string content,
        float[]? queryEmbedding = null,
        object? sources = null,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
      try
    {
            // Verify conversation exists
            var conversation = await _context.Conversations
             .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation == null)
    {
         throw new ArgumentException($"Conversation {conversationId} not found");
       }

        var message = new Message
   {
  ConversationId = conversationId,
          Role = role,
            Content = content,
     QueryEmbedding = queryEmbedding != null ? new Vector(queryEmbedding) : null,
             Sources = sources != null ? JsonDocument.Parse(JsonSerializer.Serialize(sources)) : null,
    Model = model,
      TokenCount = EstimateTokenCount(content),
            CreatedAt = DateTime.UtcNow
            };

      _context.Messages.Add(message);

      // Update conversation
            conversation.LastMessageAt = DateTime.UtcNow;
            conversation.MessageCount++;

 // Update title if it's empty and this is a user message
    if (string.IsNullOrEmpty(conversation.Title) && role == "user")
         {
                conversation.Title = content.Length > 50 ? content.Substring(0, 50) + "..." : content;
            }

      await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[ConversationService] Added {Role} message to conversation {ConversationId}", 
   role, conversationId);

  return message;
   }
    catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationService] Failed to add message to conversation {ConversationId}", 
         conversationId);
  throw;
        }
    }

    /// <summary>
    /// Get conversation history with messages
    /// </summary>
    public async Task<ConversationWithMessages?> GetConversationHistoryAsync(
   Guid conversationId,
        int? limit = null,
     CancellationToken cancellationToken = default)
    {
        try
        {
          var conversation = await _context.Conversations
          .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

    if (conversation == null)
       {
       return null;
    }

            var messagesQuery = _context.Messages
        .Where(m => m.ConversationId == conversationId)
       .OrderBy(m => m.CreatedAt);

     var messages = limit.HasValue 
            ? await messagesQuery.Take(limit.Value).ToListAsync(cancellationToken)
         : await messagesQuery.ToListAsync(cancellationToken);

       return new ConversationWithMessages
   {
                Conversation = conversation,
    Messages = messages
            };
        }
        catch (Exception ex)
        {
    _logger.LogError(ex, "[ConversationService] Failed to get conversation history {ConversationId}", 
    conversationId);
          throw;
        }
    }

    /// <summary>
    /// Get recent conversations for a user
    /// </summary>
    public async Task<List<Conversation>> GetUserConversationsAsync(
        string? userId = null,
    int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
  {
   var query = _context.Conversations
       .Where(c => c.Status == "active");

         if (!string.IsNullOrEmpty(userId))
            {
              query = query.Where(c => c.UserId == userId);
            }

            var conversations = await query
  .OrderByDescending(c => c.LastMessageAt)
       .Take(limit)
                .ToListAsync(cancellationToken);

            return conversations;
  }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ConversationService] Failed to get user conversations for {UserId}", userId);
      throw;
        }
    }

    /// <summary>
    /// Search conversations by content
    /// </summary>
    public async Task<List<ConversationSearchResult>> SearchConversationsAsync(
     string searchTerm,
        string? userId = null,
    int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
     {
      var query = from c in _context.Conversations
           join m in _context.Messages on c.Id equals m.ConversationId
     where c.Status == "active" && 
           (c.Title != null && c.Title.Contains(searchTerm) || 
             m.Content.Contains(searchTerm))
           select new ConversationSearchResult
              {
      Conversation = c,
       MatchingMessage = m,
             MatchType = c.Title != null && c.Title.Contains(searchTerm) ? "title" : "content"
             };

      if (!string.IsNullOrEmpty(userId))
          {
    query = query.Where(r => r.Conversation.UserId == userId);
          }

  var results = await query
      .OrderByDescending(r => r.Conversation.LastMessageAt)
        .Take(limit)
     .ToListAsync(cancellationToken);

       return results;
        }
        catch (Exception ex)
        {
    _logger.LogError(ex, "[ConversationService] Failed to search conversations for term '{SearchTerm}'", 
       searchTerm);
         throw;
        }
    }

    /// <summary>
    /// Archive a conversation
    /// </summary>
    public async Task<bool> ArchiveConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
      try
        {
            var conversation = await _context.Conversations
        .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

  if (conversation == null)
            {
return false;
       }

   conversation.Status = "archived";
   await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("[ConversationService] Archived conversation {ConversationId}", conversationId);
   return true;
     }
        catch (Exception ex)
    {
      _logger.LogError(ex, "[ConversationService] Failed to archive conversation {ConversationId}", 
  conversationId);
 throw;
    }
    }

    /// <summary>
    /// Get conversation statistics
    /// </summary>
    public async Task<ConversationStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return new ConversationStats
      {
       TotalConversations = await _context.Conversations.CountAsync(cancellationToken),
       ActiveConversations = await _context.Conversations.CountAsync(c => c.Status == "active", cancellationToken),
         TotalMessages = await _context.Messages.CountAsync(cancellationToken),
            UserMessages = await _context.Messages.CountAsync(m => m.Role == "user", cancellationToken),
  AssistantMessages = await _context.Messages.CountAsync(m => m.Role == "assistant", cancellationToken),
      MessagesWithEmbeddings = await _context.Messages.CountAsync(m => m.QueryEmbedding != null, cancellationToken)
        };
  }

    private static int EstimateTokenCount(string text)
    {
     // Simple token estimation: ~4 characters per token
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}

/// <summary>
/// Conversation with its messages
/// </summary>
public class ConversationWithMessages
{
    public Conversation Conversation { get; set; } = null!;
    public List<Message> Messages { get; set; } = new();
}

/// <summary>
/// Conversation search result
/// </summary>
public class ConversationSearchResult
{
    public Conversation Conversation { get; set; } = null!;
    public Message MatchingMessage { get; set; } = null!;
    public string MatchType { get; set; } = string.Empty; // "title" or "content"
}

/// <summary>
/// Conversation statistics
/// </summary>
public class ConversationStats
{
    public int TotalConversations { get; set; }
    public int ActiveConversations { get; set; }
    public int TotalMessages { get; set; }
    public int UserMessages { get; set; }
    public int AssistantMessages { get; set; }
    public int MessagesWithEmbeddings { get; set; }
}