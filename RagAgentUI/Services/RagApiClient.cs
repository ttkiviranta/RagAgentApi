using RagAgentUI.Models;
using System.Text.Json;

namespace RagAgentUI.Services;

public class RagApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;

    public RagApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _httpClient.BaseAddress = new Uri(configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    // Ingest
    public async Task<IngestResponse> IngestUrlAsync(string url)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/rag/ingest-enhanced", new { url });
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IngestResponse>(content, _jsonOptions) ??
            new IngestResponse(Guid.Empty, "Error processing response", "error");
    }

    // Query (non-streaming, fallback)
    public async Task<QueryResponse> QueryAsync(Guid conversationId, string query)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/rag/query", new { conversationId, query });
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<QueryResponse>(content, _jsonOptions) ??
            new QueryResponse("Error processing response", new List<SourceDto>());
    }

    // Conversations
    public async Task<List<ConversationDto>> GetConversationsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/conversations");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<ConversationDto>>(content, _jsonOptions) ?? new List<ConversationDto>();
            }
            return new List<ConversationDto>();
        }
        catch
        {
            return new List<ConversationDto>();
        }
    }

    public async Task<CreateConversationResponse> CreateConversationAsync(string? title = null)
    {
        var request = new { Title = title };
        var response = await _httpClient.PostAsJsonAsync("/api/conversations", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CreateConversationResponse>(content, _jsonOptions)
            ?? throw new Exception("Failed to deserialize response");
    }

    public async Task<List<MessageDto>> GetConversationHistoryAsync(Guid conversationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/conversations/{conversationId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<MessageDto>>(content, _jsonOptions) ?? new List<MessageDto>();
            }
            return new List<MessageDto>();
        }
        catch
        {
            return new List<MessageDto>();
        }
    }

    // Agent Analytics
    public async Task<AgentStatsResponse> GetAgentStatsAsync(int days = 7)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/agent-analytics?days={days}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AgentStatsResponse>(content, _jsonOptions) ??
                    new AgentStatsResponse(new List<AgentStatDto>());
            }
            // Return mock data for testing
            return new AgentStatsResponse(new List<AgentStatDto>
            {
                new("OrchestratorAgent", 25, 1500, 0.95),
                new("ScraperAgent", 22, 2300, 0.88),
                new("ChunkerAgent", 25, 800, 0.98),
                new("EmbeddingAgent", 25, 1200, 0.96),
                new("PostgresStorageAgent", 25, 500, 0.99)
            });
        }
        catch
        {
            return new AgentStatsResponse(new List<AgentStatDto>());
        }
    }
}