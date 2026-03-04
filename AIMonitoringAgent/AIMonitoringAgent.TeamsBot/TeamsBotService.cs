using AdaptiveCards.Templating;
using AIMonitoringAgent.Shared.Models;
using AIMonitoringAgent.Shared.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace AIMonitoringAgent.TeamsBot;

public interface ITeamsBotService
{
    Task<Activity> HandleActivityAsync(Activity activity);
    Task<List<ChatMessage>> GetConversationHistoryAsync(string conversationId);
    Task SaveConversationMessageAsync(ChatMessage message);
}

public class TeamsBotService : ITeamsBotService
{
    private readonly IVectorMemoryStore _vectorMemory;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILlmAnalyzer _llmAnalyzer;
    private readonly ILogger<TeamsBotService> _logger;
    private readonly Dictionary<string, List<ChatMessage>> _conversationHistory;

    public TeamsBotService(
        IVectorMemoryStore vectorMemory,
        IEmbeddingService embeddingService,
        ILlmAnalyzer llmAnalyzer,
        ILogger<TeamsBotService> logger)
    {
        _vectorMemory = vectorMemory;
        _embeddingService = embeddingService;
        _llmAnalyzer = llmAnalyzer;
        _logger = logger;
        _conversationHistory = new Dictionary<string, List<ChatMessage>>();
    }

    public async Task<Activity> HandleActivityAsync(Activity activity)
    {
        try
        {
            _logger.LogInformation(
                "Handling Teams activity: Type={Type}, From={From}",
                activity.Type,
                activity.From?.Name);

            return activity.Type switch
            {
                ActivityTypes.Message => await HandleMessageActivityAsync(activity),
                ActivityTypes.ConversationUpdate => HandleConversationUpdate(activity),
                _ => HandleDefaultActivity(activity)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Teams activity");
            throw;
        }
    }

    private async Task<Activity> HandleMessageActivityAsync(Activity activity)
    {
        var userMessage = activity.Text ?? string.Empty;
        var conversationId = activity.Conversation?.Id ?? Guid.NewGuid().ToString();

        _logger.LogInformation("Processing message: {Message}", userMessage[..Math.Min(100, userMessage.Length)]);

        // Save user message
        var chatMessage = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "user",
            Content = userMessage,
            Timestamp = DateTime.UtcNow
        };
        await SaveConversationMessageAsync(chatMessage);

        // Generate embedding and search
        var embedding = await _embeddingService.GenerateEmbeddingAsync(userMessage);
        var relevantErrors = await _vectorMemory.SearchSimilarErrorsAsync(embedding, topK: 5);

        // Generate response
        var responseText = GenerateResponse(userMessage, relevantErrors);

        // Save bot response
        var botMessage = new ChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            Content = responseText,
            Timestamp = DateTime.UtcNow
        };
        await SaveConversationMessageAsync(botMessage);

        // Create adaptive card response
        var adaptiveCard = BuildAdaptiveCardResponse(userMessage, relevantErrors);

        var reply = MessageFactory.Attachment(new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = adaptiveCard
        });

        reply.Text = responseText;

        return reply;
    }

    private Activity HandleConversationUpdate(Activity activity)
    {
        var conversationId = activity.Conversation?.Id ?? Guid.NewGuid().ToString();
        _logger.LogInformation("Conversation update: {ConversationId}", conversationId);

        // Initialize conversation history
        if (!_conversationHistory.ContainsKey(conversationId))
        {
            _conversationHistory[conversationId] = new List<ChatMessage>();
        }

        var reply = MessageFactory.Text(
            "Welcome to the AI Monitoring Agent! I can help you analyze errors and identify patterns. Try asking:\n" +
            "• Show me all errors related to SQL timeouts\n" +
            "• Is this error new or recurring?\n" +
            "• What changed before this error started?\n" +
            "• Did this error correlate with a deployment?");

        return reply;
    }

    private Activity HandleDefaultActivity(Activity activity)
    {
        _logger.LogWarning("Unknown activity type: {Type}", activity.Type);
        return MessageFactory.Text("I don't know how to handle this type of activity.");
    }

    public async Task<List<ChatMessage>> GetConversationHistoryAsync(string conversationId)
    {
        if (_conversationHistory.TryGetValue(conversationId, out var history))
        {
            return history;
        }

        return new List<ChatMessage>();
    }

    public async Task SaveConversationMessageAsync(ChatMessage message)
    {
        if (!_conversationHistory.ContainsKey(message.ConversationId))
        {
            _conversationHistory[message.ConversationId] = new List<ChatMessage>();
        }

        _conversationHistory[message.ConversationId].Add(message);

        // In a real implementation, also persist to database
        await Task.CompletedTask;
    }

    private string GenerateResponse(string userQuery, List<VectorMemoryRecord> relevantErrors)
    {
        // Analyze the query to determine intent
        var lowerQuery = userQuery.ToLower();

        if (lowerQuery.Contains("sql") && lowerQuery.Contains("timeout"))
        {
            var sqlErrors = relevantErrors
                .Where(e => e.Message.Contains("SQL", StringComparison.OrdinalIgnoreCase) ||
                           e.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sqlErrors.Any())
            {
                return $"Found {sqlErrors.Count} SQL timeout errors:\n" +
                       string.Join("\n", sqlErrors.Take(3).Select(e =>
                           $"• {e.ExceptionType}: {e.Message} (Occurred {e.OccurrenceCount} times)"));
            }
            else
            {
                return "No SQL timeout errors found in recent history.";
            }
        }

        if (lowerQuery.Contains("new") || lowerQuery.Contains("recurring"))
        {
            if (relevantErrors.Any())
            {
                var recurring = relevantErrors.FirstOrDefault(e => e.OccurrenceCount > 1);
                if (recurring != null)
                {
                    return $"This error is recurring - it has occurred {recurring.OccurrenceCount} times. " +
                           $"Root cause: {recurring.RootCauseAnalysis}";
                }
                else
                {
                    return "This appears to be a new error we haven't seen before.";
                }
            }
        }

        if (lowerQuery.Contains("deployment") || lowerQuery.Contains("changed"))
        {
            if (relevantErrors.Any() && !string.IsNullOrEmpty(relevantErrors.First().DeploymentId))
            {
                return $"This error may be correlated with deployment {relevantErrors.First().DeploymentId}. " +
                       "Please check the deployment timeline.";
            }
            else
            {
                return "No deployment correlation found for these errors.";
            }
        }

        // Default response
        return $"I found {relevantErrors.Count} related errors:\n" +
               string.Join("\n", relevantErrors.Take(3).Select(e =>
                   $"• {e.ExceptionType}: {e.Message} (Severity: {e.Severity})"));
    }

    private object BuildAdaptiveCardResponse(string userQuery, List<VectorMemoryRecord> relevantErrors)
    {
        var errorBlocks = new List<object>();

        if (relevantErrors.Any())
        {
            foreach (var error in relevantErrors.Take(3))
            {
                errorBlocks.Add(new
                {
                    type = "Container",
                    style = "accent",
                    items = new object[] {
                        new {
                            type = "TextBlock",
                            text = error.ExceptionType,
                            weight = "bolder",
                            wrap = true
                        },
                        new {
                            type = "FactSet",
                            facts = new object[] {
                                new { name = "Message:", value = error.Message },
                                new { name = "Severity:", value = error.Severity },
                                new { name = "Occurrences:", value = error.OccurrenceCount.ToString() },
                                new { name = "Category:", value = error.Category }
                            }
                        },
                        new {
                            type = "TextBlock",
                            text = $"Root Cause: {error.RootCauseAnalysis}",
                            wrap = true,
                            spacing = "medium",
                            size = "small"
                        }
                    }
                });
            }
        }
        else
        {
            errorBlocks.Add(new {
                type = "TextBlock",
                text = "No related errors found.",
                wrap = true
            });
        }

        return new
        {
            schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[] {
                new {
                    type = "TextBlock",
                    text = "Error Analysis Results",
                    weight = "bolder",
                    size = "large"
                },
                new {
                    type = "TextBlock",
                    text = $"Query: {userQuery}",
                    wrap = true,
                    spacing = "medium"
                }
            }.Concat(errorBlocks).ToArray()
        };
    }
}
