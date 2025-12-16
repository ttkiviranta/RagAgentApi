using Microsoft.AspNetCore.SignalR;
using RagAgentApi.Services;
using RagAgentApi.Data;
using RagAgentApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace RagAgentApi.Hubs;

public class ChatHub : Hub
{
    private readonly PostgresQueryService _queryService;
    private readonly IAzureOpenAIService _openAI;
    private readonly ConversationService _conversationService;
    private readonly RagDbContext _db;
    private readonly ILogger<ChatHub> _logger;
    private readonly IConfiguration _configuration;
    
    public ChatHub(
        PostgresQueryService queryService, 
        IAzureOpenAIService openAI, 
        ConversationService conversationService,
        RagDbContext db,
        ILogger<ChatHub> logger,
        IConfiguration configuration)
    {
        _queryService = queryService;
        _openAI = openAI;
        _conversationService = conversationService;
        _db = db;
        _logger = logger;
        _configuration = configuration;
    }
    
    public async Task StreamQuery(string query, Guid conversationId)
    {
        try
        {
            // Get conversation at the beginning with tracking
            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);
                
            if (conversation == null)
            {
                await Clients.Caller.SendAsync("ReceiveError", "Conversation not found");
                return;
            }
            
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
            
            // Update conversation metadata for user message
            conversation.LastMessageAt = DateTime.UtcNow;
            conversation.MessageCount++;
            
            _logger.LogInformation("[ChatHub] Incremented MessageCount to {Count} for conversation {ConversationId}", 
                conversation.MessageCount, conversationId);
            
            await _db.SaveChangesAsync();
            
            // Get embedding for query
            var queryEmbedding = await _openAI.GetEmbeddingAsync(query);
            
            // Vector search using PostgresQueryService (minScore 0.5 = 50% similarity)
            var searchResults = await _queryService.SearchAsync(queryEmbedding, topK: 5, minScore: 0.5);
            
            _logger.LogInformation("[ChatHub] Found {Count} search results for query: '{Query}'", 
                searchResults.Count, query);
            
            string fullAnswer;
            List<SourceDto> sources;
            
            // Check RAG mode from configuration (default: hybrid)
            var ragMode = _configuration.GetValue<string>("RagSettings:Mode", "hybrid")?.ToLower();
            
            // Check if we found any relevant documents
            if (!searchResults.Any())
            {
                _logger.LogWarning("[ChatHub] No documents found in database for query: '{Query}'", query);
                
                if (ragMode == "strict")
                {
                    // Strict mode: Only answer from documents
                    fullAnswer = "Kontekstissa ei ole tietoa tähän kysymykseen. " +
                                "Varmista että olet ensin ladannut dokumentteja järjestelmään käyttämällä 'Ingest Document' -toimintoa.";
                    
                    // Stream the response word by word
                    var words = fullAnswer.Split(' ');
                    foreach (var word in words)
                    {
                        await Clients.Caller.SendAsync("ReceiveChunk", word + " ");
                        await Task.Delay(50);
                    }
                }
                else
                {
                    // Hybrid mode: Use general ChatGPT knowledge with disclaimer
                    var disclaimerPrefix = "?? Dokumenteista ei löytynyt tietoa. Vastaan yleisen tietämykseni perusteella:\n\n";
                    
                    // Send disclaimer first
                    await Clients.Caller.SendAsync("ReceiveChunk", disclaimerPrefix);
                    await Task.Delay(100);
                    
                    // Use ChatGPT without context
                    var systemPrompt = @"You are a helpful AI assistant. Answer the user's question based on your general knowledge.
Be concise, accurate, and helpful. If you're not certain about something, say so.";
                    
                    fullAnswer = disclaimerPrefix;
                    await foreach (var chunk in _openAI.GetChatCompletionStreamAsync(query, ""))
                    {
                        fullAnswer += chunk;
                        await Clients.Caller.SendAsync("ReceiveChunk", chunk);
                    }
                    
                    _logger.LogInformation("[ChatHub] Generated answer using general knowledge (no context)");
                }
                
                sources = new List<SourceDto>();
            }
            else
            {
                // Documents found - use RAG with context
                var context = string.Join("\n\n", searchResults.Select(r => r.Content));
                
                _logger.LogDebug("[ChatHub] Context length: {Length} characters", context.Length);
                
                // Add prefix to indicate document-based answer
                var prefix = "?? Vastaus dokumenttien perusteella:\n\n";
                await Clients.Caller.SendAsync("ReceiveChunk", prefix);
                fullAnswer = prefix;
                
                // Stream answer from Azure OpenAI with context
                await foreach (var chunk in _openAI.GetChatCompletionStreamAsync(query, context))
                {
                    fullAnswer += chunk;
                    await Clients.Caller.SendAsync("ReceiveChunk", chunk);
                }
                
                // Convert search results to SourceDto with proper relevance scores
                sources = searchResults.Select(r => new SourceDto(
                    Url: r.SourceUrl ?? "",
                    Content: r.Content.Length > 100 ? r.Content[..100] + "..." : r.Content,
                    RelevanceScore: r.RelevanceScore
                )).ToList();
                
                _logger.LogInformation("[ChatHub] Generated answer with {SourceCount} sources", sources.Count);
            }
            
            // Save assistant message
            var assistantMessage = new RagAgentApi.Models.PostgreSQL.Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = "assistant",
                Content = fullAnswer,
                CreatedAt = DateTime.UtcNow,
                Sources = sources.Any() 
                    ? JsonDocument.Parse(JsonSerializer.Serialize(sources))
                    : null
            };
            
            _db.Messages.Add(assistantMessage);
            
            // Update conversation metadata for assistant message
            conversation.MessageCount++;
            conversation.LastMessageAt = DateTime.UtcNow;
            
            _logger.LogInformation("[ChatHub] Final MessageCount: {Count} for conversation {ConversationId}", 
                conversation.MessageCount, conversationId);
            
            await _db.SaveChangesAsync();
            
            await Clients.Caller.SendAsync("ReceiveComplete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChatHub] Error processing query: '{Query}'", query);
            await Clients.Caller.SendAsync("ReceiveError", $"Virhe: {ex.Message}");
        }
    }
}