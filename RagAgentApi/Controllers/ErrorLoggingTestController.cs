using RagAgentApi.Services;
using AIMonitoringAgent.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace RagAgentApi.Controllers;

/// <summary>
/// Test endpoints for error logging functionality
/// </summary>
[ApiController]
[Route("api/error-logging-test")]
public class ErrorLoggingTestController : ControllerBase
{
    private readonly IErrorLogService _errorLogService;
    private readonly ILogger<ErrorLoggingTestController> _logger;

    public ErrorLoggingTestController(
        IErrorLogService errorLogService,
        ILogger<ErrorLoggingTestController> logger)
    {
        _errorLogService = errorLogService;
        _logger = logger;
    }

    /// <summary>
    /// Test error logging to database
    /// </summary>
    /// <returns>Created error log entry</returns>
    [HttpPost("log-test-error")]
    public async Task<IActionResult> LogTestError()
    {
        try
        {
            // Create a test exception
            var testException = new AppInsightsException
            {
                ExceptionType = "Test.DatabaseLogging",
                Message = "This is a test error to verify database logging is working correctly.",
                StackTrace = "at RagAgentApi.Controllers.ErrorLoggingTestController.LogTestError()",
                Timestamp = DateTime.UtcNow,
                OperationName = "TestErrorLogging",
                RequestId = Guid.NewGuid().ToString().Substring(0, 8)
            };

            // Create a test analysis result
            var testAnalysis = new AnalysisResult
            {
                ErrorId = "test-db-" + Guid.NewGuid().ToString().Substring(0, 8),
                Severity = "INFO",
                Category = "Test",
                RootCauseAnalysis = "Testing error logging to PostgreSQL database.",
                RecommendedActions = new List<string>
                {
                    "Verify error appears in ErrorLogs table",
                    "Check timestamp and error details",
                    "Confirm all fields are populated correctly"
                },
                AffectedOperations = new List<string> { "ErrorLogging.Test" },
                IsRecurring = false,
                SimilarErrorCount = 0,
                AffectedUsers = 0,
                Timestamp = DateTime.UtcNow
            };

            // Log the error to database
            var errorLog = await _errorLogService.LogErrorAsync(
                testAnalysis,
                testException,
                new[] { "test" });

            _logger.LogInformation(
                "Test error logged successfully: {ErrorId}",
                errorLog.ErrorId);

            return Ok(new
            {
                message = "Test error logged to database successfully",
                errorLog = new
                {
                    id = errorLog.Id,
                    errorId = errorLog.ErrorId,
                    exceptionType = errorLog.ExceptionType,
                    severity = errorLog.Severity,
                    timestamp = errorLog.Timestamp,
                    notificationSent = errorLog.NotificationSent
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log test error to database");
            return StatusCode(500, new
            {
                error = "Failed to log test error",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Get all logged errors
    /// </summary>
    /// <param name="limit">Maximum number of errors to return</param>
    /// <param name="offset">Offset for pagination</param>
    /// <returns>List of error logs</returns>
    [HttpGet("errors")]
    public async Task<IActionResult> GetErrors([FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        try
        {
            var errors = await _errorLogService.GetErrorsAsync(limit, offset);

            return Ok(new
            {
                count = errors.Count,
                limit,
                offset,
                errors = errors.Select(e => new
                {
                    id = e.Id,
                    errorId = e.ErrorId,
                    exceptionType = e.ExceptionType,
                    severity = e.Severity,
                    category = e.Category,
                    message = e.Message,
                    timestamp = e.Timestamp,
                    notificationSent = e.NotificationSent,
                    notificationChannels = e.NotificationChannels
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get errors");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get error by ID
    /// </summary>
    /// <param name="errorId">Error ID to retrieve</param>
    /// <returns>Error log entry</returns>
    [HttpGet("errors/{errorId}")]
    public async Task<IActionResult> GetErrorById(string errorId)
    {
        try
        {
            var error = await _errorLogService.GetErrorByIdAsync(errorId);

            if (error == null)
            {
                return NotFound(new { error = "Error not found", errorId });
            }

            return Ok(new
            {
                id = error.Id,
                errorId = error.ErrorId,
                exceptionType = error.ExceptionType,
                message = error.Message,
                severity = error.Severity,
                category = error.Category,
                rootCauseAnalysis = error.RootCauseAnalysis,
                recommendedActions = error.RecommendedActions,
                affectedOperations = error.AffectedOperations,
                operationName = error.OperationName,
                requestId = error.RequestId,
                isRecurring = error.IsRecurring,
                similarErrorCount = error.SimilarErrorCount,
                affectedUsers = error.AffectedUsers,
                timestamp = error.Timestamp,
                notificationSent = error.NotificationSent,
                notificationChannels = error.NotificationChannels,
                stackTrace = error.StackTrace
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get error by ID: {ErrorId}", errorId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get errors by severity
    /// </summary>
    /// <param name="severity">Severity level (INFO, WARNING, ERROR, CRITICAL)</param>
    /// <returns>List of errors with specified severity</returns>
    [HttpGet("errors/by-severity/{severity}")]
    public async Task<IActionResult> GetErrorsBySeverity(string severity)
    {
        try
        {
            var errors = await _errorLogService.GetErrorsByUrgencyAsync(severity);

            return Ok(new
            {
                severity,
                count = errors.Count,
                errors = errors.Select(e => new
                {
                    id = e.Id,
                    errorId = e.ErrorId,
                    exceptionType = e.ExceptionType,
                    message = e.Message,
                    timestamp = e.Timestamp
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get errors by severity: {Severity}", severity);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get errors within date range
    /// </summary>
    /// <param name="startDate">Start date (ISO 8601)</param>
    /// <param name="endDate">End date (ISO 8601)</param>
    /// <returns>List of errors within date range</returns>
    [HttpGet("errors/by-date")]
    public async Task<IActionResult> GetErrorsByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            var errors = await _errorLogService.GetErrorsByDateRangeAsync(startDate, endDate);

            return Ok(new
            {
                startDate,
                endDate,
                count = errors.Count,
                errors = errors.Select(e => new
                {
                    id = e.Id,
                    errorId = e.ErrorId,
                    severity = e.Severity,
                    timestamp = e.Timestamp
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get errors by date range");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete old errors
    /// </summary>
    /// <param name="daysOld">Delete errors older than this many days</param>
    /// <returns>Number of deleted errors</returns>
    [HttpDelete("errors/cleanup")]
    public async Task<IActionResult> DeleteOldErrors([FromQuery] int daysOld = 30)
    {
        try
        {
            var deletedCount = await _errorLogService.DeleteOldErrorsAsync(daysOld);

            return Ok(new
            {
                message = "Old errors deleted successfully",
                daysOld,
                deletedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old errors");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
