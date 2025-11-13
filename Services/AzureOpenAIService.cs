using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.Options;

namespace RagAgentApi.Services;

public class AzureOpenAIService : IAzureOpenAIService
{
    private readonly OpenAIClient _client;
    private readonly AzureOpenAIConfig _config;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AzureOpenAIService(IConfiguration configuration, ILogger<AzureOpenAIService> logger)
    {
        _logger = logger;
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
}