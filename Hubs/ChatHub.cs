using Microsoft.AspNetCore.SignalR;
using RagAgentApi.Services;
using RagAgentApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace RagAgentApi.Hubs;

public class ChatHub : Hub
{
  private readonly PostgresQueryService _queryService;
    private readonly IAzureOpenAIService _openAI;
    private readonly ConversationService _conversationService;
    private readonly RagDbContext _db;
    
    public ChatHub(
        PostgresQueryService queryService, 
IAzureOpenAIService openAI, 
      ConversationService conversationService,
        RagDbContext db)
    {
    _queryService = queryService;
        _openAI = openAI;
        _conversationService = conversationService;
        _db = db;
    }
    
    public async Task StreamQuery(string query, Guid conversationId)
    {
      try
        {
  // Save user message
        var userMessage = new RagAgentApi.Models.PostgreSQL.Message
            {
      Id = Guid.NewGuid(),
 ConversationId = conversationId,
   Role = "user",
     Content = query,
   CreatedAt = DateTime.UtcNow
  };
      
            _db.Messages.Add(userMessage);
  await _db.SaveChangesAsync();
     
   // Update conversation last message time
    var conversation = await _db.Conversations.FindAsync(conversationId);
    if (conversation != null)
{
         conversation.LastMessageAt = DateTime.UtcNow;
       await _db.SaveChangesAsync();
   }
     
            // Get embedding for query
          var queryEmbedding = await _openAI.GetEmbeddingAsync(query);
        
     // Vector search using PostgresQueryService
    var searchResults = await _queryService.SearchAsync(queryEmbedding, topK: 5);
   
        // Build context from search results
    var context = string.Join("\n\n", searchResults.Select(r => r.Content));
        
   // Stream answer from Azure OpenAI
   var fullAnswer = "";
       await foreach (var chunk in _openAI.GetChatCompletionStreamAsync(query, context))
      {
     fullAnswer += chunk;
    await Clients.Caller.SendAsync("ReceiveChunk", chunk);
            }
 
   // Save assistant message
   var assistantMessage = new RagAgentApi.Models.PostgreSQL.Message
   {
       Id = Guid.NewGuid(),
  ConversationId = conversationId,
         Role = "assistant",
         Content = fullAnswer,
        CreatedAt = DateTime.UtcNow,
         Sources = JsonDocument.Parse(
    JsonSerializer.Serialize(
        searchResults.Select(r => new { 
            Url = r.SourceUrl, 
            Content = r.Content.Length > 100 ? r.Content[..100] + "..." : r.Content, 
            RelevanceScore = r.RelevanceScore 
        }).ToList()
    )
)
       };
            
        _db.Messages.Add(assistantMessage);
       await _db.SaveChangesAsync();
      
            await Clients.Caller.SendAsync("ReceiveComplete");
        }
   catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ReceiveError", $"Error: {ex.Message}");
        }
    }
}