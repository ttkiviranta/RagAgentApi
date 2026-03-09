using AIMonitoringAgent.Shared.Models;
using AIMonitoringAgent.Shared.Services;
using RagAgentApi.Services;
using System.Net;
using System.Text.Json;

namespace RagAgentApi.Middleware;

/// <summary>
/// Global exception handling middleware that captures all unhandled exceptions,
/// logs them to the database, and optionally sends notifications.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Log to database
        try
        {
            using var scope = context.RequestServices.CreateScope();
            var errorLogService = scope.ServiceProvider.GetService<IErrorLogService>();
            var emailNotifier = scope.ServiceProvider.GetService<IEmailNotifier>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            if (errorLogService != null)
            {
                var severity = DetermineSeverity(exception);
                var analysis = new AnalysisResult
                {
                    ErrorId = "api-" + Guid.NewGuid().ToString().Substring(0, 8),
                    Severity = severity,
                    Category = DetermineCategory(exception),
                    RootCauseAnalysis = $"Unhandled exception in {context.Request.Path}: {exception.Message}",
                    RecommendedActions = GenerateRecommendations(exception),
                    AffectedOperations = new List<string> { context.Request.Path.ToString() },
                    IsRecurring = false,
                    SimilarErrorCount = 0,
                    AffectedUsers = 1,
                    Timestamp = DateTime.UtcNow
                };

                var appInsightsException = new AppInsightsException
                {
                    ExceptionType = exception.GetType().Name,
                    Message = exception.Message,
                    StackTrace = exception.StackTrace ?? "No stack trace available",
                    Timestamp = DateTime.UtcNow,
                    OperationName = $"{context.Request.Method} {context.Request.Path}",
                    RequestId = context.TraceIdentifier
                };

                // Determine notification channels
                var notificationChannels = new List<string>();
                
                // Send email for critical/error severity
                if (emailNotifier != null && (severity == "CRITICAL" || severity == "ERROR"))
                {
                    try
                    {
                        var recipients = configuration.GetSection("NotificationConfigs")
                            .Get<List<NotificationConfig>>()?
                            .Where(n => n.Channel == "email" && n.Enabled)
                            .SelectMany(n => n.Recipients ?? new List<string>())
                            .ToList() ?? new List<string>();

                        if (recipients.Any())
                        {
                            await emailNotifier.SendAsync(analysis, appInsightsException, recipients);
                            notificationChannels.Add("email");
                            _logger.LogInformation("Error notification sent to {Count} recipients", recipients.Count);
                        }
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, "Failed to send error notification email");
                    }
                }

                // Save to database
                await errorLogService.LogErrorAsync(analysis, appInsightsException, notificationChannels.ToArray());
                _logger.LogInformation("Error logged to database: {ErrorId}", analysis.ErrorId);
            }
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to log exception to database or send notification");
        }

        // Return error response
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            error = "An unexpected error occurred",
            message = exception.Message,
            traceId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private string DetermineSeverity(Exception exception)
    {
        return exception switch
        {
            OutOfMemoryException => "CRITICAL",
            StackOverflowException => "CRITICAL",
            AccessViolationException => "CRITICAL",
            UnauthorizedAccessException => "ERROR",
            InvalidOperationException => "ERROR",
            ArgumentException => "WARNING",
            KeyNotFoundException => "WARNING",
            _ => "ERROR"
        };
    }

    private string DetermineCategory(Exception exception)
    {
        var typeName = exception.GetType().Name;
        
        if (typeName.Contains("Sql") || typeName.Contains("Db") || typeName.Contains("Database"))
            return "Database";
        if (typeName.Contains("Http") || typeName.Contains("Network") || typeName.Contains("Socket"))
            return "Network";
        if (typeName.Contains("Auth") || typeName.Contains("Security") || typeName.Contains("Permission"))
            return "Security";
        if (typeName.Contains("IO") || typeName.Contains("File") || typeName.Contains("Directory"))
            return "FileSystem";
        if (typeName.Contains("Timeout"))
            return "Timeout";
        if (typeName.Contains("Json") || typeName.Contains("Serializ"))
            return "Serialization";
            
        return "Application";
    }

    private List<string> GenerateRecommendations(Exception exception)
    {
        var recommendations = new List<string>
        {
            "Review the stack trace for the root cause",
            "Check application logs for more context"
        };

        var typeName = exception.GetType().Name;

        if (typeName.Contains("Sql") || typeName.Contains("Db"))
        {
            recommendations.Add("Verify database connection string");
            recommendations.Add("Check database server availability");
        }
        else if (typeName.Contains("Http") || typeName.Contains("Network"))
        {
            recommendations.Add("Check network connectivity");
            recommendations.Add("Verify external service endpoints");
        }
        else if (typeName.Contains("Auth"))
        {
            recommendations.Add("Verify authentication credentials");
            recommendations.Add("Check API keys and tokens");
        }
        else if (typeName.Contains("NullReference"))
        {
            recommendations.Add("Add null checks before accessing objects");
            recommendations.Add("Review object initialization logic");
        }

        return recommendations;
    }
}

/// <summary>
/// Extension method to register the middleware
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
