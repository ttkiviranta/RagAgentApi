using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.Options;

namespace RagAgentApi.Services;

public class AzureOpenAIService : IAzureOpenAIService
{
    private readonly OpenAIClient _client;
    private readonly AzureOpenAIConfig _config;
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AzureOpenAIService(
        IConfiguration configuration, 
        ILogger<AzureOpenAIService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _config = new AzureOpenAIConfig();
    configuration.GetSection("Azure:OpenAI").Bind(_config);

      _client = new OpenAIClient(
new Uri(_config.Endpoint),
            new AzureKeyCredential(_config.Key));
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
 {
       var options = new EmbeddingsOptions(_config.EmbeddingDeployment, new[] { text });
      var response = await _client.GetEmbeddingsAsync(options, cancellationToken);

            var embedding = response.Value.Data[0].Embedding.ToArray();
        
_logger.LogDebug("Generated embedding with {Dimensions} dimensions for text of length {Length}",
                embedding.Length, text.Length);

        return embedding;
        }
        catch (Exception ex)
 {
_logger.LogError(ex, "Failed to generate embedding for text of length {Length}", text.Length);

       // Log to error dashboard
       _ = LogErrorToDashboardAsync(ex, "OpenAI.GetEmbedding", 
           $"Failed to generate embedding: {ex.Message}");

       throw;
        }
}

    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
     const int batchSize = 100;
        var embeddings = new List<float[]>();

        for (int i = 0; i < texts.Count; i += batchSize)
   {
    var batch = texts.Skip(i).Take(batchSize).ToList();
            var batchEmbeddings = await GetBatchEmbeddingsAsync(batch, cancellationToken);
  embeddings.AddRange(batchEmbeddings);

 _logger.LogDebug("Processed embedding batch {BatchNumber}/{TotalBatches} ({BatchSize} items)",
       (i / batchSize) + 1, (int)Math.Ceiling((double)texts.Count / batchSize), batch.Count);
        }

        _logger.LogInformation("Generated {Count} embeddings total", embeddings.Count);
        return embeddings;
    }

    private async Task<List<float[]>> GetBatchEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
 var delays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };

 for (int attempt = 0; attempt < maxRetries; attempt++)
        {
      try
          {
         var options = new EmbeddingsOptions(_config.EmbeddingDeployment, texts);
 var response = await _client.GetEmbeddingsAsync(options, cancellationToken);

          return response.Value.Data.Select(d => d.Embedding.ToArray()).ToList();
  }
            catch (RequestFailedException ex) when (attempt < maxRetries - 1)
       {
     _logger.LogWarning(ex, "Attempt {Attempt} failed for batch embedding, retrying in {Delay}ms",
       attempt + 1, delays[attempt].TotalMilliseconds);
   await Task.Delay(delays[attempt], cancellationToken);
    }
        }

        throw new InvalidOperationException($"Failed to get embeddings after {maxRetries} attempts");
    }

    public async Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
   const int maxRetries = 3;
 var delays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };

        for (int attempt = 0; attempt < maxRetries; attempt++)
    {
  try
            {
     var chatCompletionsOptions = new ChatCompletionsOptions()
            {
 DeploymentName = _config.ChatDeployment,
             Messages =
       {
   new ChatRequestSystemMessage(systemPrompt),
  new ChatRequestUserMessage(userPrompt)
      },
         MaxTokens = _config.MaxTokens,
            Temperature = _config.Temperature
        };

      var response = await _client.GetChatCompletionsAsync(chatCompletionsOptions, cancellationToken);
        var content = response.Value.Choices[0].Message.Content;

          _logger.LogDebug("Generated chat completion with {TokenCount} tokens",
   response.Value.Usage.TotalTokens);

        return content ?? string.Empty;
            }
            catch (RequestFailedException ex) when (attempt < maxRetries - 1)
            {
      _logger.LogWarning(ex, "Attempt {Attempt} failed for chat completion, retrying in {Delay}ms",
      attempt + 1, delays[attempt].TotalMilliseconds);
     await Task.Delay(delays[attempt], cancellationToken);
      }
        }

        throw new InvalidOperationException($"Failed to get chat completion after {maxRetries} attempts");
    }

    public async IAsyncEnumerable<string> GetChatCompletionStreamAsync(string userPrompt, string context, CancellationToken cancellationToken = default)
    {
        // Build system prompt based on whether context is available
        string systemPrompt;

        if (string.IsNullOrWhiteSpace(context))
        {
            // No context - use general ChatGPT knowledge
            systemPrompt = @"You are a helpful AI assistant. Answer the user's question based on your general knowledge.
Be concise, accurate, and helpful. If you're not certain about something, clearly state your level of confidence.
Provide practical and useful information.
IMPORTANT: Always respond in the same language as the user's question.";
        }
        else
        {
            // Context available - HYBRID mode: prefer documents, fallback to general knowledge
            systemPrompt = $@"You are a helpful AI assistant. You have access to some context documents below.

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

        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = _config.ChatDeployment,
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(userPrompt)
            },
            MaxTokens = _config.MaxTokens,
            Temperature = _config.Temperature
        };

    IAsyncEnumerable<string> StreamContentAsync()
    {
        return StreamContentInternalAsync();
    }

    async IAsyncEnumerable<string> StreamContentInternalAsync()
    {
        bool errorOccurred = false;
        string? errorMessage = null;
        List<string> results = new();

        try
        {
            var response = await _client.GetChatCompletionsStreamingAsync(chatCompletionsOptions, cancellationToken);

            await foreach (var choice in response.WithCancellation(cancellationToken))
            {
                if (choice.ContentUpdate is not null)
                {
                    results.Add(choice.ContentUpdate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get streaming chat completion");
            errorOccurred = true;
            errorMessage = $"Error: {ex.Message}";
        }

        foreach (var item in results)
        {
            yield return item;
        }

        if (errorOccurred && errorMessage is not null)
        {
            yield return errorMessage;
        }
    }

    await foreach (var item in StreamContentAsync().WithCancellation(cancellationToken))
    {
        yield return item;
    }
}

    private class AzureOpenAIConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string EmbeddingDeployment { get; set; } = string.Empty;
        public string ChatDeployment { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public int MaxTokens { get; set; } = 8192;
        public float Temperature { get; set; } = 0.7f;
    }

    private async Task LogErrorToDashboardAsync(Exception ex, string operationName, string message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var errorLogService = scope.ServiceProvider.GetService<IErrorLogService>();

            if (errorLogService != null)
            {
                await errorLogService.LogErrorAsync(
                    message: message,
                    category: "AzureOpenAI",
                    severity: "ERROR",
                    operationName: operationName,
                    requestId: null);
            }
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "[AzureOpenAI] Failed to log error to dashboard");
        }
    }
}