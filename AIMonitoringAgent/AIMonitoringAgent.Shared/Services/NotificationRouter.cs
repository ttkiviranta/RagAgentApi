using AIMonitoringAgent.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AIMonitoringAgent.Shared.Services;

public interface INotificationRouter
{
    Task RouteNotificationAsync(
        AnalysisResult analysis,
        AppInsightsException exception,
        List<NotificationConfig> notificationConfigs);
}

public interface INotifier
{
    Task SendAsync(AnalysisResult analysis, AppInsightsException exception);
}

public class NotificationRouter : INotificationRouter
{
    private readonly IEmailNotifier _emailNotifier;
    private readonly ITeamsNotifier _teamsNotifier;
    private readonly ISlackNotifier _slackNotifier;
    private readonly ILogger<NotificationRouter> _logger;

    public NotificationRouter(
        IEmailNotifier emailNotifier,
        ITeamsNotifier teamsNotifier,
        ISlackNotifier slackNotifier,
        ILogger<NotificationRouter> logger)
    {
        _emailNotifier = emailNotifier;
        _teamsNotifier = teamsNotifier;
        _slackNotifier = slackNotifier;
        _logger = logger;
    }

    public async Task RouteNotificationAsync(
        AnalysisResult analysis,
        AppInsightsException exception,
        List<NotificationConfig> notificationConfigs)
    {
        try
        {
            var tasks = new List<Task>();

            foreach (var config in notificationConfigs.Where(c => c.Enabled))
            {
                try
                {
                    switch (config.Channel.ToLowerInvariant())
                    {
                        case "email":
                            tasks.Add(_emailNotifier.SendAsync(analysis, exception, config.Recipients));
                            break;
                        case "teams":
                            if (!string.IsNullOrEmpty(config.WebhookUrl))
                            {
                                tasks.Add(_teamsNotifier.SendAsync(analysis, exception, config.WebhookUrl));
                            }
                            break;
                        case "slack":
                            if (!string.IsNullOrEmpty(config.WebhookUrl))
                            {
                                tasks.Add(_slackNotifier.SendAsync(analysis, exception, config.WebhookUrl));
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to route notification to {Channel}", config.Channel);
                }
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("Routed notifications for error {ErrorId}", analysis.ErrorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to route notifications");
        }
    }
}

public interface IEmailNotifier : INotifier
{
    Task SendAsync(AnalysisResult analysis, AppInsightsException exception, List<string> recipients);
}

public class EmailNotifier : IEmailNotifier
{
    private readonly ILogger<EmailNotifier> _logger;
    private readonly IConfiguration _configuration;

    public EmailNotifier(
        ILogger<EmailNotifier> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendAsync(AnalysisResult analysis, AppInsightsException exception)
    {
        _logger.LogWarning("Email notifier requires recipients list - use SendAsync(analysis, exception, recipients) instead");
        await Task.CompletedTask;
    }

    public async Task SendAsync(AnalysisResult analysis, AppInsightsException exception, List<string> recipients)
    {
        try
        {
            var smtpServer = _configuration.GetValue<string>("EmailSettings:SmtpServer");
            var smtpPort = _configuration.GetValue<int>("EmailSettings:SmtpPort", 587);
            var username = _configuration.GetValue<string>("EmailSettings:Username")?.Trim();
            var password = _configuration.GetValue<string>("EmailSettings:Password")?.Trim();
            var fromAddress = _configuration.GetValue<string>("EmailSettings:FromAddress");
            var fromName = _configuration.GetValue<string>("EmailSettings:FromName", "RAG Agent");

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Email settings not fully configured. Skipping email notification.");
                return;
            }

            // Use MailKit for proper STARTTLS support on port 587
            using (var client = new MailKit.Net.Smtp.SmtpClient())
            {
                try
                {
                    // Connect to SMTP server
                    // Port 587 = SMTP + STARTTLS (not SSL)
                    // Port 465 = SMTPS (SSL from the start)
                    await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);

                    _logger.LogDebug("Connected to SMTP server: {Server}:{Port}", smtpServer, smtpPort);

                    // Authenticate with credentials
                    await client.AuthenticateAsync(username, password);

                    _logger.LogDebug("Authenticated as {Username}", username);

                    // Build email message
                    var emailMessage = new MimeKit.MimeMessage();
                    emailMessage.From.Add(new MimeKit.MailboxAddress(fromName, fromAddress));
                    emailMessage.Subject = $"[{analysis.Severity.ToUpper()}] {analysis.Category}: {exception.ExceptionType}";

                    // Add recipients
                    foreach (var recipient in recipients)
                    {
                        emailMessage.To.Add(new MimeKit.MailboxAddress("", recipient));
                    }

                    // Set body
                    emailMessage.Body = new MimeKit.TextPart("plain")
                    {
                        Text = BuildEmailBody(analysis, exception)
                    };

                    // Send email
                    await client.SendAsync(emailMessage);

                    _logger.LogInformation(
                        "Error notification email sent to {Recipients} with subject: {Subject}",
                        string.Join(", ", recipients),
                        emailMessage.Subject);
                }
                finally
                {
                    // Gracefully disconnect
                    await client.DisconnectAsync(true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification");
            throw;
        }
    }

    private static string BuildEmailBody(AnalysisResult analysis, AppInsightsException exception)
    {
        return $@"
RAG Agent Error Notification
============================

Error ID: {analysis.ErrorId}
Severity: {analysis.Severity}
Category: {analysis.Category}
Timestamp: {analysis.Timestamp:O}

Exception Details
-----------------
Type: {exception.ExceptionType}
Message: {exception.Message}
Operation: {exception.OperationName}
Request ID: {exception.RequestId}

Root Cause Analysis
-------------------
{analysis.RootCauseAnalysis}

Recommended Actions
-------------------
{string.Join("\n", analysis.RecommendedActions.Select(a => $"- {a}"))}

Affected Operations
-------------------
{string.Join("\n", analysis.AffectedOperations.Select(o => $"- {o}"))}

Additional Info
---------------
Is Recurring: {analysis.IsRecurring}
Similar Errors: {analysis.SimilarErrorCount}
Affected Users: {analysis.AffectedUsers}

Stack Trace
-----------
{exception.StackTrace}
";
    }
}

public interface ITeamsNotifier : INotifier
{
    Task SendAsync(AnalysisResult analysis, AppInsightsException exception, string webhookUrl);
}

public class TeamsNotifier : ITeamsNotifier
{
    private readonly ILogger<TeamsNotifier> _logger;
    private readonly HttpClient _httpClient;

    public TeamsNotifier(ILogger<TeamsNotifier> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task SendAsync(AnalysisResult analysis, AppInsightsException exception)
    {
        _logger.LogWarning("Teams notifier called without webhook URL");
        await Task.CompletedTask;
    }

    public async Task SendAsync(AnalysisResult analysis, AppInsightsException exception, string webhookUrl)
    {
        try
        {
            var payload = BuildAdaptiveCard(analysis, exception);
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(webhookUrl, content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Teams notification sent for error {ErrorId}", analysis.ErrorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Teams notification");
            throw;
        }
    }

    private static object BuildAdaptiveCard(AnalysisResult analysis, AppInsightsException exception)
    {
        var severityColor = analysis.Severity switch
        {
            "critical" => "FF0000",
            "high" => "FF6600",
            "medium" => "FFCC00",
            _ => "00CC00"
        };

        return new
        {
            @type = "message",
            attachments = new[] {
                new {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    contentUrl = (string?)null,
                    content = new {
                        schema = "http://adaptivecards.io/schemas/adaptive-card.json",
                        type = "AdaptiveCard",
                        version = "1.5",
                        body = new object[] {
                            new {
                                type = "Container",
                                style = "emphasis",
                                items = new object[] {
                                    new {
                                        type = "ColumnSet",
                                        columns = new object[] {
                                            new {
                                                width = "stretch",
                                                items = new object[] {
                                                    new {
                                                        type = "TextBlock",
                                                        text = $"🚨 {analysis.Severity.ToUpper()} - {analysis.Category}",
                                                        weight = "bolder",
                                                        size = "large",
                                                        color = $"accent"
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            new {
                                type = "FactSet",
                                facts = new object[] {
                                    new { name = "Exception Type:", value = exception.ExceptionType },
                                    new { name = "Operation:", value = exception.OperationName },
                                    new { name = "Time:", value = exception.Timestamp.ToString("O") },
                                    new { name = "Request ID:", value = exception.RequestId },
                                    new { name = "Is Recurring:", value = analysis.IsRecurring ? "Yes" : "No" },
                                    new { name = "Similar Errors:", value = analysis.SimilarErrorCount.ToString() }
                                }
                            },
                            new {
                                type = "TextBlock",
                                text = "Root Cause",
                                weight = "bolder",
                                size = "medium",
                                spacing = "medium"
                            },
                            new {
                                type = "TextBlock",
                                text = analysis.RootCauseAnalysis,
                                wrap = true,
                                spacing = "small"
                            },
                            new {
                                type = "TextBlock",
                                text = "Recommended Actions",
                                weight = "bolder",
                                size = "medium",
                                spacing = "medium"
                            },
                            new {
                                type = "TextBlock",
                                text = string.Join("\n", analysis.RecommendedActions.Select(a => $"• {a}")),
                                wrap = true,
                                spacing = "small"
                            }
                        }
                    }
                }
            }
        };
    }
}

public interface ISlackNotifier : INotifier
{
    Task SendAsync(AnalysisResult analysis, AppInsightsException exception, string webhookUrl);
}

public class SlackNotifier : ISlackNotifier
{
    private readonly ILogger<SlackNotifier> _logger;
    private readonly HttpClient _httpClient;

    public SlackNotifier(ILogger<SlackNotifier> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task SendAsync(AnalysisResult analysis, AppInsightsException exception)
    {
        _logger.LogWarning("Slack notifier called without webhook URL");
        await Task.CompletedTask;
    }

    public async Task SendAsync(AnalysisResult analysis, AppInsightsException exception, string webhookUrl)
    {
        try
        {
            var payload = BuildSlackMessage(analysis, exception);
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(webhookUrl, content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Slack notification sent for error {ErrorId}", analysis.ErrorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");
            throw;
        }
    }

    private static object BuildSlackMessage(AnalysisResult analysis, AppInsightsException exception)
    {
        var severityColor = analysis.Severity switch
        {
            "critical" => "danger",
            "high" => "warning",
            "medium" => "#FF9900",
            _ => "good"
        };

        return new
        {
            text = $"🚨 {analysis.Severity.ToUpper()} - {analysis.Category} Error",
            blocks = new object[] {
                new {
                    type = "header",
                    text = new {
                        type = "plain_text",
                        text = $"🚨 {analysis.Severity.ToUpper()} - {exception.ExceptionType}"
                    }
                },
                new {
                    type = "section",
                    fields = new object[] {
                        new { type = "mrkdwn", text = $"*Operation:*\n{exception.OperationName}" },
                        new { type = "mrkdwn", text = $"*Category:*\n{analysis.Category}" },
                        new { type = "mrkdwn", text = $"*Recurring:*\n{(analysis.IsRecurring ? "Yes" : "No")}" },
                        new { type = "mrkdwn", text = $"*Similar Errors:*\n{analysis.SimilarErrorCount}" }
                    }
                },
                new {
                    type = "section",
                    text = new {
                        type = "mrkdwn",
                        text = $"*Root Cause:*\n{analysis.RootCauseAnalysis}"
                    }
                },
                new {
                    type = "section",
                    text = new {
                        type = "mrkdwn",
                        text = $"*Recommended Actions:*\n{string.Join("\n", analysis.RecommendedActions.Select(a => $"• {a}"))}"
                    }
                }
            }
        };
    }
}
