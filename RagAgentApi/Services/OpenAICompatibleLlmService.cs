using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RagAgentApi.Services;

/// <summary>
/// OpenAI-compatible LLM service for cloud endpoints like Groq, Together, Fireworks, OpenRouter
/// Supports models like Qwen 2.5 via OpenAI-compatible chat.completions API
/// </summary>
public class OpenAICompatibleLlmService : IOpenAICompatibleLlmService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAICompatibleConfig _config;
    private readonly ILogger<OpenAICompatibleLlmService> _logger;

    public OpenAICompatibleLlmService(
        IConfiguration configuration,
        ILogger<OpenAICompatibleLlmService> logger)
    {
        _logger = logger;
        _config = new OpenAICompatibleConfig();
        configuration.GetSection("LlmProviders:OpenAICompatible").Bind(_config);

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RagAgentApi/1.0");
    }

    public async Task<string> GetChatCompletionAsync(string userPrompt, string context = "", CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("[OpenAICompatibleLlm] Getting chat completion for prompt of length {Length}", userPrompt.Length);

            var systemPrompt = BuildSystemPrompt(context);
            var request = BuildChatRequest(systemPrompt, userPrompt, stream: false);

            var response = await SendRequestAsync(request, cancellationToken);

            if (response?.Choices?.Count > 0)
            {
                var content = response.Choices[0].Message?.Content ?? string.Empty;
                _logger.LogDebug("[OpenAICompatibleLlm] Got completion response with {TokenCount} tokens", response.Usage?.TotalTokens ?? 0);
                return content;
            }

            _logger.LogError("[OpenAICompatibleLlm] No choices in response");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAICompatibleLlm] Error getting chat completion");
            throw;
        }
    }

    public async IAsyncEnumerable<string> GetChatCompletionStreamAsync(string userPrompt, string context = "", CancellationToken cancellationToken = default)
    {
        var systemPrompt = BuildSystemPrompt(context);
        var request = BuildChatRequest(systemPrompt, userPrompt, stream: true);

        _logger.LogDebug("[OpenAICompatibleLlm] Starting streamed chat completion");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.BaseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[OpenAICompatibleLlm] API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                throw new InvalidOperationException($"API returned {response.StatusCode}: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAICompatibleLlm] Error starting stream chat completion");
            throw;
        }

        using (response)
        using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
        using (var reader = new StreamReader(stream))
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line == ": OPENROUTER PROCESSING")
                    continue;

                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);

                    if (data == "[DONE]")
                        break;

                    var content = ExtractContentFromDelta(data);
                    if (!string.IsNullOrEmpty(content))
                    {
                        yield return content;
                    }
                }
            }
        }
    }

    private string? ExtractContentFromDelta(string data)
    {
        try
        {
            var delta = JsonSerializer.Deserialize<StreamDeltaResponse>(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (delta?.Choices?.Count > 0 && !string.IsNullOrEmpty(delta.Choices[0].Delta?.Content))
            {
                return delta.Choices[0].Delta.Content;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[OpenAICompatibleLlm] Failed to parse stream delta: {Data}", data);
        }
        return null;
    }

    private string BuildSystemPrompt(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return @"You are a helpful AI assistant. Answer the user's question based on your general knowledge.
Be concise, accurate, and helpful. If you're not certain about something, clearly state your level of confidence.
Provide practical and useful information.
IMPORTANT: Always respond in the same language as the user's question.";
        }

        return $@"You are a helpful AI assistant. You have access to some context documents below.

INSTRUCTIONS:
1. First, check if the provided context contains information relevant to the user's question
2. If the context DOES contain relevant information: Answer based on the context and cite it
3. If the context does NOT contain relevant information: 
   - Start your answer with: ""[General knowledge] ""
   - Then answer based on your general knowledge
   - Be helpful and provide accurate information
4. Always respond in the same language as the user's question
5. Be concise and helpful

Context (may or may not be relevant):
{context}

Now answer the user's question. If the context is not relevant, use your general knowledge with the [General knowledge] prefix.";
    }

    private ChatCompletionRequest BuildChatRequest(string systemPrompt, string userPrompt, bool stream)
    {
        return new ChatCompletionRequest
        {
            Model = _config.ModelName,
            Messages = new[]
            {
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt }
            },
            MaxTokens = _config.MaxTokens,
            Temperature = _config.Temperature,
            Stream = stream
        };
    }

    private async Task<ChatCompletionResponse?> SendRequestAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var delays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(request);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.BaseUrl)
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };

                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (attempt < maxRetries - 1)
                    {
                        _logger.LogWarning("[OpenAICompatibleLlm] Attempt {Attempt} failed with {StatusCode}, retrying in {Delay}ms",
                            attempt + 1, response.StatusCode, delays[attempt].TotalMilliseconds);
                        await Task.Delay(delays[attempt], cancellationToken);
                        continue;
                    }

                    throw new InvalidOperationException($"API returned {response.StatusCode}: {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<ChatCompletionResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                _logger.LogWarning(ex, "[OpenAICompatibleLlm] Attempt {Attempt} failed with network error, retrying in {Delay}ms",
                    attempt + 1, delays[attempt].TotalMilliseconds);
                await Task.Delay(delays[attempt], cancellationToken);
            }
        }

        throw new InvalidOperationException($"Failed to get chat completion after {maxRetries} attempts");
    }

    private class OpenAICompatibleConfig
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ModelName { get; set; } = "qwen-2.5-7b";
        public int MaxTokens { get; set; } = 8192;
        public float Temperature { get; set; } = 0.7f;
    }

    private class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }

        [JsonPropertyName("delta")]
        public Delta? Delta { get; set; }
    }

    private class Delta
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class StreamDeltaResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Usage
    {
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}
