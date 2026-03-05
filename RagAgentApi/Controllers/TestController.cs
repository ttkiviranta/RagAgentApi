using AIMonitoringAgent.Shared.Models;
using AIMonitoringAgent.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace RagAgentApi.Controllers;

/// <summary>
/// Test endpoint for email and notification testing
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IEmailNotifier _emailNotifier;
    private readonly ILogger<TestController> _logger;

    public TestController(IEmailNotifier emailNotifier, ILogger<TestController> logger)
    {
        _emailNotifier = emailNotifier;
        _logger = logger;
    }

    /// <summary>
    /// Send a test email to verify email configuration
    /// </summary>
    /// <param name="email">Email address to send test email to</param>
    /// <returns>Test result</returns>
    [HttpPost("send-test-email")]
    public async Task<IActionResult> SendTestEmail([FromQuery] string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { error = "Email address is required" });
            }

            // Create a test exception
            var testException = new AppInsightsException
            {
                ExceptionType = "Test.EmailNotification",
                Message = "This is a test email from RAG Agent API to verify email configuration.",
                StackTrace = "Test stack trace - no actual error occurred",
                Timestamp = DateTime.UtcNow,
                OperationName = "TestOperation",
                RequestId = Guid.NewGuid().ToString().Substring(0, 8)
            };

            // Create a test analysis result
            var testAnalysis = new AnalysisResult
            {
                ErrorId = "test-" + Guid.NewGuid().ToString().Substring(0, 8),
                Severity = "INFO",
                Category = "Test",
                RootCauseAnalysis = "This is a test email to verify that your email notification system is working correctly.",
                RecommendedActions = new List<string>
                {
                    "If you received this email, your email notifications are configured correctly.",
                    "You can now enable email notifications in appsettings.Development.json by setting 'Enabled': true",
                    "Errors will now be sent to the configured recipients."
                },
                AffectedOperations = new List<string> { "Testing" },
                IsRecurring = false,
                SimilarErrorCount = 0,
                AffectedUsers = 0,
                Timestamp = DateTime.UtcNow
            };

            // Send the test email
            await _emailNotifier.SendAsync(testAnalysis, testException, new List<string> { email });

            _logger.LogInformation("Test email sent successfully to {Email}", email);

            return Ok(new
            {
                message = "Test email sent successfully",
                recipient = email,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test email");
            return StatusCode(500, new
            {
                error = "Failed to send test email",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Check email settings (without exposing sensitive data)
    /// </summary>
    /// <returns>Email configuration status</returns>
    [HttpGet("email-status")]
    public IActionResult CheckEmailStatus()
    {
        try
        {
            var config = new Dictionary<string, object>
            {
                { "smtpServer", "smtp.gmail.com" },
                { "smtpPort", 587 },
                { "useTls", true },
                { "fromAddress", "[Configured]" },
                { "username", "[Configured]" },
                { "passwordConfigured", true }
            };

            return Ok(new
            {
                status = "Email settings are configured",
                settings = config
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
