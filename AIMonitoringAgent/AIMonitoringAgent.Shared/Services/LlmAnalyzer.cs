using System.Text.Json;
using Azure.AI.OpenAI;
using AIMonitoringAgent.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AIMonitoringAgent.Shared.Services;

public interface ILlmAnalyzer
{
    Task<AnalysisResult> AnalyzeErrorAsync(
        AppInsightsException exception,
        List<VectorMemoryRecord>? similarErrors = null,
        DeploymentCorrelation? deploymentCorrelation = null);
}

public class LlmAnalyzer : ILlmAnalyzer
{
    private readonly OpenAIClient _openAiClient;
    private readonly ILogger<LlmAnalyzer> _logger;
    private readonly string _deploymentName;

    public LlmAnalyzer(
        OpenAIClient openAiClient,
        ILogger<LlmAnalyzer> logger,
        string deploymentName = "gpt-4o")
    {
        _openAiClient = openAiClient;
        _logger = logger;
        _deploymentName = deploymentName;
    }

    public async Task<AnalysisResult> AnalyzeErrorAsync(
        AppInsightsException exception,
        List<VectorMemoryRecord>? similarErrors = null,
        DeploymentCorrelation? deploymentCorrelation = null)
    {
        try
        {
            var systemPrompt = @"You are an expert AI system for analyzing application errors and exceptions. 
Your task is to analyze errors from Application Insights and provide structured analysis.
Always respond with valid JSON matching the specified schema.
Be concise but thorough in your analysis.";

            var userPrompt = BuildAnalysisPrompt(exception, similarErrors, deploymentCorrelation);

            var options = new ChatCompletionsOptions
            {
                Temperature = 0.3f,
                MaxTokens = 2000
            };

            options.Messages.Add(new ChatCompletionMessage(ChatRole.System, systemPrompt));
            options.Messages.Add(new ChatCompletionMessage(ChatRole.User, userPrompt));

            var response = await _openAiClient.GetChatCompletionsAsync(
                _deploymentName,
                options);

            var content = response.Value.Choices[0].Message.Content;
            var result = ParseAnalysisResult(content, exception);

            _logger.LogInformation(
                "LLM analysis completed for {ExceptionType}: {Severity}",
                exception.ExceptionType,
                result.Severity);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze error with LLM");
            throw;
        }
    }

    private string BuildAnalysisPrompt(
        AppInsightsException exception,
        List<VectorMemoryRecord>? similarErrors = null,
        DeploymentCorrelation? deploymentCorrelation = null)
    {
        var prompt = $@"
Analyze the following error and provide structured JSON output with this exact schema:
{{
  ""errorId"": ""unique identifier"",
  ""severity"": ""critical|high|medium|low"",
  ""category"": ""database|network|authentication|configuration|business-logic|external-service|other"",
  ""rootCauseAnalysis"": ""detailed explanation of likely root cause"",
  ""isRecurring"": boolean,
  ""similarErrorCount"": number,
  ""recommendedActions"": [""action1"", ""action2"", ...],
  ""affectedUsers"": number,
  ""affectedOperations"": [""operation1"", ...]
}}

ERROR DETAILS:
- Exception Type: {exception.ExceptionType}
- Message: {exception.Message}
- Operation: {exception.OperationName}
- Timestamp: {exception.Timestamp:O}
- Request ID: {exception.RequestId}
- Stack Trace: {exception.StackTrace}

CUSTOM DIMENSIONS:
{FormatDictionary(exception.CustomDimensions)}

CUSTOM PROPERTIES:
{FormatDictionary(exception.CustomProperties)}
";

        if (exception.DependencyInfo != null)
        {
            prompt += $@"
DEPENDENCY INFO:
- Type: {exception.DependencyInfo.Type}
- Target: {exception.DependencyInfo.Target}
- Name: {exception.DependencyInfo.Name}
- Success: {exception.DependencyInfo.Success}
- Duration: {exception.DependencyInfo.Duration}ms
";
        }

        if (similarErrors?.Any() == true)
        {
            prompt += $@"
SIMILAR PAST ERRORS ({similarErrors.Count}):
{FormatSimilarErrors(similarErrors)}
";
        }

        if (deploymentCorrelation != null)
        {
            prompt += $@"
DEPLOYMENT CORRELATION:
- Deployment: {deploymentCorrelation.ReleaseName}
- Time to Error: {deploymentCorrelation.TimeToErrorMinutes} minutes
- Commit: {deploymentCorrelation.CommitHash}
- Author: {deploymentCorrelation.Author}
- Changed Files: {string.Join(", ", deploymentCorrelation.ChangedFiles)}
- Likely Cause: {deploymentCorrelation.IsLikeCause}
";
        }

        return prompt;
    }

    private AnalysisResult ParseAnalysisResult(string jsonContent, AppInsightsException exception)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            var result = new AnalysisResult
            {
                ErrorId = exception.RequestId,
                Severity = GetString(root, "severity"),
                Category = GetString(root, "category"),
                RootCauseAnalysis = GetString(root, "rootCauseAnalysis"),
                IsRecurring = GetBoolean(root, "isRecurring"),
                SimilarErrorCount = GetInt(root, "similarErrorCount"),
                AffectedUsers = GetInt(root, "affectedUsers"),
                RecommendedActions = GetStringArray(root, "recommendedActions"),
                AffectedOperations = GetStringArray(root, "affectedOperations"),
                Timestamp = DateTime.UtcNow
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response, returning default analysis");
            return new AnalysisResult
            {
                ErrorId = exception.RequestId,
                Severity = "medium",
                Category = "other",
                RootCauseAnalysis = exception.Message,
                IsRecurring = false,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private string GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private bool GetBoolean(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind == JsonValueKind.True;
        }
        return false;
    }

    private int GetInt(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number)
        {
            return property.GetInt32();
        }
        return 0;
    }

    private List<string> GetStringArray(JsonElement element, string propertyName)
    {
        var list = new List<string>();
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString() ?? string.Empty);
            }
        }

        return list;
    }

    private static string FormatDictionary(Dictionary<string, object> dict)
    {
        if (dict.Count == 0) return "None";
        return string.Join("\n", dict.Select(x => $"  - {x.Key}: {x.Value}"));
    }

    private static string FormatDictionary(Dictionary<string, string> dict)
    {
        if (dict.Count == 0) return "None";
        return string.Join("\n", dict.Select(x => $"  - {x.Key}: {x.Value}"));
    }

    private static string FormatSimilarErrors(List<VectorMemoryRecord> errors)
    {
        return string.Join("\n", errors.Take(3).Select((e, i) =>
            $"  {i + 1}. {e.ExceptionType}: {e.Message} (Occurred {e.OccurrenceCount} times)"));
    }
}
