using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AIMonitoringAgent.Shared.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts);
}

public class EmbeddingService : IEmbeddingService
{
    private readonly OpenAIClient _openAiClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _deploymentName;

    public EmbeddingService(
        OpenAIClient openAiClient,
        ILogger<EmbeddingService> logger,
        string deploymentName = "text-embedding-3-small")
    {
        _openAiClient = openAiClient;
        _logger = logger;
        _deploymentName = deploymentName;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var response = await _openAiClient.GetEmbeddingsAsync(_deploymentName, new EmbeddingsOptions { Input = { text } });

            var embedding = response.Value.Data[0].Embedding;
            _logger.LogDebug("Generated embedding for text of length {Length}", text.Length);

            return embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding");
            throw;
        }
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        try
        {
            var results = new List<float[]>();

            // Process in batches to respect API limits
            var batchSize = 10;
            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize).ToList();

                var embeddingsOptions = new EmbeddingsOptions();
                foreach (var text in batch)
                {
                    embeddingsOptions.Input.Add(text);
                }

                var response = await _openAiClient.GetEmbeddingsAsync(_deploymentName, embeddingsOptions);

                foreach (var item in response.Value.Data)
                {
                    results.Add(item.Embedding.ToArray());
                }
            }

            _logger.LogInformation("Generated {Count} embeddings", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings");
            throw;
        }
    }
}
