using System.Text.Json;
using AIMonitoringAgent.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AIMonitoringAgent.Shared.Services;

public interface IErrorParser
{
    AppInsightsException ParseExceptionEvent(string eventData);
    List<AppInsightsException> ParseBatchEvents(string[] eventDataArray);
}

public class ErrorParser : IErrorParser
{
    private readonly ILogger<ErrorParser> _logger;

    public ErrorParser(ILogger<ErrorParser> logger)
    {
        _logger = logger;
    }

    public AppInsightsException ParseExceptionEvent(string eventData)
    {
        try
        {
            using var document = JsonDocument.Parse(eventData);
            var root = document.RootElement;

            var exception = new AppInsightsException
            {
                Timestamp = GetDateTime(root, "timestamp"),
                OperationName = GetString(root, "operationName"),
                ExceptionType = GetString(root, "exceptionType"),
                Message = GetString(root, "message"),
                StackTrace = GetString(root, "stackTrace"),
                RequestId = GetString(root, "requestId"),
                CustomDimensions = GetDictionary(root, "customDimensions"),
                CustomProperties = GetStringDictionary(root, "customProperties"),
                DependencyInfo = GetDependencyInfo(root, "dependencyInfo")
            };

            _logger.LogInformation(
                "Parsed exception: {ExceptionType} - {Message}",
                exception.ExceptionType,
                exception.Message);

            return exception;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse exception event");
            throw;
        }
    }

    public List<AppInsightsException> ParseBatchEvents(string[] eventDataArray)
    {
        var exceptions = new List<AppInsightsException>();
        
        foreach (var eventData in eventDataArray)
        {
            try
            {
                exceptions.Add(ParseExceptionEvent(eventData));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse individual event in batch");
            }
        }

        return exceptions;
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

    private DateTime GetDateTime(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(property.GetString(), out var dateTime))
            {
                return dateTime;
            }
        }
        return DateTime.UtcNow;
    }

    private Dictionary<string, object> GetDictionary(JsonElement element, string propertyName)
    {
        var dict = new Dictionary<string, object>();

        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (var property2 in property.EnumerateObject())
        {
            dict[property2.Name] = property2.Value.ValueKind switch
            {
                JsonValueKind.String => property2.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => property2.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => property2.Value.GetRawText()
            };
        }

        return dict;
    }

    private Dictionary<string, string> GetStringDictionary(JsonElement element, string propertyName)
    {
        var dict = new Dictionary<string, string>();

        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (var property2 in property.EnumerateObject())
        {
            dict[property2.Name] = property2.Value.GetString() ?? string.Empty;
        }

        return dict;
    }

    private DependencyInfo? GetDependencyInfo(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new DependencyInfo
        {
            Type = GetString(property, "type"),
            Target = GetString(property, "target"),
            Name = GetString(property, "name"),
            Success = GetBoolean(property, "success"),
            Duration = GetDouble(property, "duration")
        };
    }

    private bool GetBoolean(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind == JsonValueKind.True;
        }
        return false;
    }

    private double GetDouble(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number)
        {
            return property.GetDouble();
        }
        return 0.0;
    }
}
