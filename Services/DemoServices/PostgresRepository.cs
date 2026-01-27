using System.Text.Json;
using RagAgentApi.Data;
using RagAgentApi.Models.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace RagAgentApi.Services.DemoServices;

/// <summary>
/// Repository for PostgreSQL-based demo data and results persistence
/// </summary>
public class PostgresRepository : ITestDataRepository
{
    private readonly ILogger<PostgresRepository> _logger;
    private readonly RagDbContext _context;

    public PostgresRepository(ILogger<PostgresRepository> logger, RagDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Gets test data metadata from database
    /// </summary>
    public async Task<string> GetTestDataAsync(string demoType)
    {
        try
        {
            var testData = await _context.DemoTestData
                .Where(d => d.DemoType == demoType)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            if (testData == null)
            {
                _logger.LogWarning("[PostgresRepository] Test data not found for {DemoType}", demoType);
                return string.Empty;
            }

            _logger.LogInformation("[PostgresRepository] Retrieved test data for {DemoType} from database", demoType);
            return testData.FilePath; // Return path to actual data
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PostgresRepository] Error retrieving test data for {DemoType}", demoType);
            throw;
        }
    }

    /// <summary>
    /// Saves demo execution result to database
    /// </summary>
    public async Task SaveDemoResultAsync(string demoType, DemoResult result)
    {
        try
        {
            var execution = new DemoExecution
            {
                Id = Guid.NewGuid(),
                DemoType = demoType,
                Success = result.Success,
                Message = result.Message,
                ResultData = JsonSerializer.Serialize(result.Data),
                ExecutionTimeMs = long.Parse(result.ExecutionTimeMs.Replace("ms", "")),
                CreatedAt = DateTime.UtcNow
            };

            _context.DemoExecutions.Add(execution);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[PostgresRepository] Saved demo result for {DemoType} to database", demoType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PostgresRepository] Error saving demo result for {DemoType}", demoType);
            // Don't throw - result saving is not critical
        }
    }

    /// <summary>
    /// Retrieves recent demo results from database
    /// </summary>
    public async Task<List<DemoResult>> GetDemoResultsAsync(string demoType, int count = 10)
    {
        try
        {
            var executions = await _context.DemoExecutions
                .Where(e => e.DemoType == demoType)
                .OrderByDescending(e => e.CreatedAt)
                .Take(count)
                .ToListAsync();

            var results = new List<DemoResult>();

            foreach (var execution in executions)
            {
                try
                {
                    object? data = null;
                    if (!string.IsNullOrEmpty(execution.ResultData))
                    {
                        data = JsonSerializer.Deserialize<object>(execution.ResultData);
                    }

                    results.Add(new DemoResult
                    {
                        DemoType = execution.DemoType,
                        Success = execution.Success,
                        Message = execution.Message,
                        Data = data,
                        ExecutionTimeMs = $"{execution.ExecutionTimeMs}ms"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PostgresRepository] Error deserializing result {Id}", execution.Id);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PostgresRepository] Error retrieving demo results for {DemoType}", demoType);
            return new List<DemoResult>();
        }
    }
}
