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
            _logger.LogInformation("[ChatHub] StreamQuery called with query: '{Query}', conversationId: {ConvId}", 
                query.Substring(0, Math.Min(50, query.Length)), conversationId);

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
                    _logger.LogInformation("[ChatHub] HYBRID MODE: No documents found, using general knowledge for query: '{Query}'", query);

                    var disclaimerPrefix = "[No documents found] Answering based on general knowledge:\n\n";

                    // Send disclaimer first
                    _logger.LogInformation("[ChatHub] Sending disclaimer prefix: {Prefix}", disclaimerPrefix);
                    await Clients.Caller.SendAsync("ReceiveChunk", disclaimerPrefix);
                    await Task.Delay(10);

                    // Use ChatGPT without context (empty string = no RAG context)
                    fullAnswer = disclaimerPrefix;
                    int chunkCount = 0;
                    await foreach (var chunk in _openAI.GetChatCompletionStreamAsync(query, ""))
                    {
                        fullAnswer += chunk;
                        chunkCount++;
                        await Clients.Caller.SendAsync("ReceiveChunk", chunk);
                        await Task.Delay(1);
                    }
                    _logger.LogInformation("[ChatHub] HYBRID MODE completed: Generated {ChunkCount} chunks with general knowledge", chunkCount);
                }

                sources = new List<SourceDto>();
            }
            else
            {
                // Documents found - use RAG with context
                var context = string.Join("\n\n", searchResults.Select(r => r.Content));
                
                _logger.LogDebug("[ChatHub] Context length: {Length} characters", context.Length);
                
                // Prepare sources immediately
                sources = searchResults.Select(r => new SourceDto(
                    Url: r.SourceUrl ?? "",
                    Content: r.Content.Length > 200 ? r.Content.Substring(0, 200) + "..." : r.Content,
                    RelevanceScore: r.RelevanceScore  // ← Fixed: RelevanceScore not Score
                )).ToList();
                
                // Send sources in real-time
                var sourcesJson = System.Text.Json.JsonSerializer.Serialize(sources);
                _logger.LogInformation("[ChatHub] Sending ReceiveSources with {Count} sources", sources.Count);
                await Clients.Caller.SendAsync("ReceiveSources", sourcesJson);

                // No prefix needed - let LLM respond naturally in user's language
                fullAnswer = "";

                // Stream answer from Azure OpenAI with context
                int chunkCount = 0;
                await foreach (var chunk in _openAI.GetChatCompletionStreamAsync(query, context))
                {
                    fullAnswer += chunk;
                    chunkCount++;
                    if (chunkCount % 5 == 0) // Log every 5 chunks
                    {
                        _logger.LogDebug("[ChatHub] Sending ReceiveChunk #{ChunkNum}: {Length} chars", chunkCount, chunk.Length);
                    }
                    await Clients.Caller.SendAsync("ReceiveChunk", chunk);
                    await Task.Delay(1); // Small delay between chunks
                }
                _logger.LogInformation("[ChatHub] Sent {ChunkCount} chunks total", chunkCount);

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

            _logger.LogInformation("[ChatHub] Sending ReceiveComplete");
            await Clients.Caller.SendAsync("ReceiveComplete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChatHub] EXCEPTION in StreamQuery: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
            await Clients.Caller.SendAsync("ReceiveError", $"Virhe: {ex.Message}");
        }
    }
}
