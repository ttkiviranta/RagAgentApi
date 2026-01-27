using System.Security.Cryptography;
using System.Text.Json;

namespace RagAgentApi.Services.DemoServices;

/// <summary>
/// Repository for local file-based test data
/// </summary>
public class LocalFileRepository : ITestDataRepository
{
    private readonly ILogger<LocalFileRepository> _logger;
    private readonly string _basePath;

    public LocalFileRepository(ILogger<LocalFileRepository> logger, IConfiguration configuration)
    {
        _logger = logger;
        var configPath = configuration["DemoSettings:LocalDataPath"] ?? "demos/";
        _basePath = Path.Combine(Directory.GetCurrentDirectory(), configPath);
    }

    /// <summary>
    /// Gets test data from local file system
    /// </summary>
    public async Task<string> GetTestDataAsync(string demoType)
    {
        try
        {
            var filePath = GetFilePath(demoType);
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("[LocalFileRepository] Test data not found for {DemoType} at {Path}", demoType, filePath);
                return string.Empty;
            }

            var content = await File.ReadAllTextAsync(filePath);
            _logger.LogInformation("[LocalFileRepository] Loaded test data for {DemoType} ({Bytes} bytes)", demoType, content.Length);
            
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LocalFileRepository] Error reading test data for {DemoType}", demoType);
            throw;
        }
    }

    /// <summary>
    /// Saves demo result to local file (for archival)
    /// </summary>
    public async Task SaveDemoResultAsync(string demoType, DemoResult result)
    {
        try
        {
            var resultsPath = Path.Combine(_basePath, demoType, "results");
            Directory.CreateDirectory(resultsPath);

            var fileName = $"result_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(resultsPath, fileName);

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            _logger.LogInformation("[LocalFileRepository] Saved demo result for {DemoType} to {Path}", demoType, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LocalFileRepository] Error saving demo result for {DemoType}", demoType);
            // Don't throw - result saving is not critical
        }
    }

    /// <summary>
    /// Retrieves recent demo results from local files
    /// </summary>
    public async Task<List<DemoResult>> GetDemoResultsAsync(string demoType, int count = 10)
    {
        try
        {
            var resultsPath = Path.Combine(_basePath, demoType, "results");
            
            if (!Directory.Exists(resultsPath))
            {
                return new List<DemoResult>();
            }

            var files = Directory.GetFiles(resultsPath, "result_*.json")
                .OrderByDescending(f => f)
                .Take(count)
                .ToList();

            var results = new List<DemoResult>();

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var result = JsonSerializer.Deserialize<DemoResult>(json);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[LocalFileRepository] Error reading result file {File}", file);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LocalFileRepository] Error retrieving demo results for {DemoType}", demoType);
            return new List<DemoResult>();
        }
    }

    private string GetFilePath(string demoType)
    {
        return demoType switch
        {
            "classification" => Path.Combine(_basePath, "classification", "data", "classification_training.csv"),
            "time-series" => Path.Combine(_basePath, "time-series", "data", "timeseries_data.csv"),
            "image" => Path.Combine(_basePath, "image-processing", "data", "test_image.png"),
            "audio" => Path.Combine(_basePath, "audio-processing", "data", "test_audio.wav"),
            _ => throw new ArgumentException($"Unknown demo type: {demoType}")
        };
    }
}
