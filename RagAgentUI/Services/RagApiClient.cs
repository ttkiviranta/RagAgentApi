using RagAgentUI.Models;
using System.Text.Json;

namespace RagAgentUI.Services;

public class RagApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<RagApiClient> _logger;

    public RagApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<RagApiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000");

        _logger.LogInformation("[RagApiClient] Initialized with BaseUrl: {BaseUrl}", _httpClient.BaseAddress);

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
            _logger.LogInformation("[RagApiClient] GetConversationsAsync called");
            _logger.LogInformation("[RagApiClient] Calling GET {Url}", $"{_httpClient.BaseAddress}api/conversations");
            
            var response = await _httpClient.GetAsync("/api/conversations");
            
            _logger.LogInformation("[RagApiClient] Response status: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("[RagApiClient] Response content length: {Length}", content.Length);
                _logger.LogDebug("[RagApiClient] Response content: {Content}", content);
                
                var conversations = JsonSerializer.Deserialize<List<ConversationDto>>(content, _jsonOptions) ?? new List<ConversationDto>();
                _logger.LogInformation("[RagApiClient] Deserialized {Count} conversations", conversations.Count);
                
                return conversations;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[RagApiClient] Failed to get conversations. Status: {Status}, Content: {Content}", 
                    response.StatusCode, errorContent);
            }
            
            return new List<ConversationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RagApiClient] Exception in GetConversationsAsync");
            return new List<ConversationDto>();
        }
    }

    public async Task<CreateConversationResponse> CreateConversationAsync(string? title = null)
    {
        _logger.LogInformation("[RagApiClient] CreateConversationAsync called with title: {Title}", title);
        
        var request = new { Title = title };
        var response = await _httpClient.PostAsJsonAsync("/api/conversations", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        _logger.LogDebug("[RagApiClient] Create conversation response: {Content}", content);
        
        return JsonSerializer.Deserialize<CreateConversationResponse>(content, _jsonOptions)
            ?? throw new Exception("Failed to deserialize response");
    }

    public async Task<List<MessageDto>> GetConversationHistoryAsync(Guid conversationId)
    {
        try
        {
            _logger.LogInformation("[RagApiClient] GetConversationHistoryAsync called for {ConversationId}", conversationId);
            
            var response = await _httpClient.GetAsync($"/api/conversations/{conversationId}");
            
            _logger.LogInformation("[RagApiClient] History response status: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("[RagApiClient] History response length: {Length}", content.Length);
                
                var messages = JsonSerializer.Deserialize<List<MessageDto>>(content, _jsonOptions) ?? new List<MessageDto>();
                _logger.LogInformation("[RagApiClient] Deserialized {Count} messages", messages.Count);
                
                return messages;
            }
            
            return new List<MessageDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RagApiClient] Exception in GetConversationHistoryAsync");
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

    // Error Logging
    public class ErrorLog
    {
        public Guid Id { get; set; }
        public string ErrorId { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string RootCauseAnalysis { get; set; } = string.Empty;
        public string RecommendedActions { get; set; } = string.Empty;
        public string AffectedOperations { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
        public string? RequestId { get; set; }
        public bool IsRecurring { get; set; }
        public int SimilarErrorCount { get; set; }
        public int AffectedUsers { get; set; }
        public DateTime Timestamp { get; set; }
        public bool NotificationSent { get; set; }
        public DateTime? NotificationSentAt { get; set; }
        public string NotificationChannels { get; set; } = string.Empty;
    }

    public class ErrorLogsResponse
    {
        public int Count { get; set; }
        public int Limit { get; set; }
        public int Offset { get; set; }
        public List<ErrorLog> Errors { get; set; } = new();
    }

    public async Task<ErrorLogsResponse> GetErrorsAsync(int limit = 50, int offset = 0)
    {
        try
        {
            _logger.LogInformation("[RagApiClient] GetErrorsAsync called with limit={Limit}, offset={Offset}", limit, offset);
            var url = $"/api/error-logging-test/errors?limit={limit}&offset={offset}";
            var fullUrl = $"{_httpClient.BaseAddress}{url}";
            _logger.LogInformation("[RagApiClient] Calling {Url}", fullUrl);

            var response = await _httpClient.GetAsync(url);
            _logger.LogInformation("[RagApiClient] Response status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("[RagApiClient] Response content length: {Length}", content.Length);
                _logger.LogDebug("[RagApiClient] Response content: {Content}", content);

                var result = JsonSerializer.Deserialize<ErrorLogsResponse>(content, _jsonOptions) ??
                    new ErrorLogsResponse();

                _logger.LogInformation("[RagApiClient] Deserialized {Count} errors", result.Errors?.Count ?? 0);
                return result;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[RagApiClient] Failed to get errors. Status: {Status}, Content: {Content}", 
                    response.StatusCode, errorContent);
            }

            return new ErrorLogsResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RagApiClient] Exception in GetErrorsAsync");
            return new ErrorLogsResponse();
        }
    }

    public async Task<ErrorLog?> GetErrorByIdAsync(string errorId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/error-logging-test/errors/{errorId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ErrorLog>(content, _jsonOptions);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RagApiClient] Failed to get error by ID: {ErrorId}", errorId);
            return null;
        }
    }

    public async Task<ErrorLogsResponse> GetErrorsBySeverityAsync(string severity)
    {
        try
        {
            _logger.LogInformation("[RagApiClient] GetErrorsBySeverityAsync called with severity={Severity}", severity);
            var url = $"/api/error-logging-test/errors/by-severity/{severity}";
            _logger.LogInformation("[RagApiClient] Calling {Url}", url);

            var response = await _httpClient.GetAsync(url);
            _logger.LogInformation("[RagApiClient] Response status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("[RagApiClient] Response content length: {Length}", content.Length);
                _logger.LogDebug("[RagApiClient] Response content: {Content}", content);

                var result = JsonSerializer.Deserialize<ErrorLogsResponse>(content, _jsonOptions) ??
                    new ErrorLogsResponse();

                _logger.LogInformation("[RagApiClient] Deserialized {Count} errors for severity {Severity}", 
                    result.Errors?.Count ?? 0, severity);
                return result;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[RagApiClient] Failed to get errors by severity. Status: {Status}, Content: {Content}", 
                response.StatusCode, errorContent);
            return new ErrorLogsResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RagApiClient] Exception in GetErrorsBySeverityAsync");
            return new ErrorLogsResponse();
        }
    }

    public async Task<bool> LogTestErrorAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/error-logging-test/log-test-error", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RagApiClient] Failed to log test error");
            return false;
        }
    }
}