using RagAgentApi.Data;
using RagAgentApi.Models;
using AIMonitoringAgent.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace RagAgentApi.Services;

/// <summary>
/// Service for managing error logs in the database
/// </summary>
public interface IErrorLogService
{
    Task<ErrorLog> LogErrorAsync(AnalysisResult analysis, AppInsightsException exception, string[] notificationChannels);
    Task<ErrorLog?> GetErrorByIdAsync(string errorId);
    Task<List<ErrorLog>> GetErrorsAsync(int limit = 100, int offset = 0);
    Task<List<ErrorLog>> GetErrorsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<List<ErrorLog>> GetErrorsByUrgencyAsync(string severity);
    Task<int> DeleteOldErrorsAsync(int daysOld = 30);
}

public class ErrorLogService : IErrorLogService
{
    private readonly RagDbContext _context;
    private readonly ILogger<ErrorLogService> _logger;

    public ErrorLogService(RagDbContext context, ILogger<ErrorLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Log an error to the database
    /// </summary>
    public async Task<ErrorLog> LogErrorAsync(AnalysisResult analysis, AppInsightsException exception, string[] notificationChannels)
    {
        try
        {
            var errorLog = new ErrorLog
            {
                Id = Guid.NewGuid(),
                ErrorId = analysis.ErrorId,
                ExceptionType = exception.ExceptionType,
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                Severity = analysis.Severity,
                Category = analysis.Category,
                RootCauseAnalysis = analysis.RootCauseAnalysis,
                RecommendedActions = JsonSerializer.Serialize(analysis.RecommendedActions),
                AffectedOperations = JsonSerializer.Serialize(analysis.AffectedOperations),
                OperationName = exception.OperationName,
                RequestId = exception.RequestId,
                IsRecurring = analysis.IsRecurring,
                SimilarErrorCount = analysis.SimilarErrorCount,
                AffectedUsers = analysis.AffectedUsers,
                Timestamp = analysis.Timestamp,
                NotificationSent = notificationChannels.Length > 0,
                NotificationSentAt = notificationChannels.Length > 0 ? DateTime.UtcNow : null,
                NotificationChannels = JsonSerializer.Serialize(notificationChannels)
            };

            _context.ErrorLogs.Add(errorLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Error logged to database: {ErrorId} - {ExceptionType}",
                errorLog.ErrorId,
                errorLog.ExceptionType);

            return errorLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log error to database");
            throw;
        }
    }

    /// <summary>
    /// Get error by ErrorId
    /// </summary>
    public async Task<ErrorLog?> GetErrorByIdAsync(string errorId)
    {
        try
        {
            return await _context.ErrorLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ErrorId == errorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error by ID: {ErrorId}", errorId);
            throw;
        }
    }

    /// <summary>
    /// Get all errors with pagination
    /// </summary>
    public async Task<List<ErrorLog>> GetErrorsAsync(int limit = 100, int offset = 0)
    {
        try
        {
            return await _context.ErrorLogs
                .AsNoTracking()
                .OrderByDescending(e => e.Timestamp)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get errors");
            throw;
        }
    }

    /// <summary>
    /// Get errors within date range
    /// </summary>
    public async Task<List<ErrorLog>> GetErrorsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            return await _context.ErrorLogs
                .AsNoTracking()
                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get errors by date range");
            throw;
        }
    }

    /// <summary>
    /// Get errors by severity level
    /// </summary>
    public async Task<List<ErrorLog>> GetErrorsByUrgencyAsync(string severity)
    {
        try
        {
            return await _context.ErrorLogs
                .AsNoTracking()
                .Where(e => e.Severity == severity)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get errors by severity: {Severity}", severity);
            throw;
        }
    }

    /// <summary>
    /// Delete errors older than specified days
    /// </summary>
    public async Task<int> DeleteOldErrorsAsync(int daysOld = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var deletedCount = await _context.ErrorLogs
                .Where(e => e.Timestamp < cutoffDate)
                .ExecuteDeleteAsync();

            _logger.LogInformation(
                "Deleted {Count} error logs older than {Days} days",
                deletedCount,
                daysOld);

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old errors");
            throw;
        }
    }
}
