using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RagAgentApi.Services;

public interface IGuardrailService
{
    /// <summary>
    /// Validate input and return (isValid, errorMessage)
    /// </summary>
    (bool IsValid, string? Error) ValidateInput(string? input, string source = "user_input");

    /// <summary>
    /// Validate output and return (isValid, errorMessage)
    /// </summary>
    (bool IsValid, string? Error) ValidateOutput(string? output, bool expectJson = false);

    /// <summary>
    /// Check for hallucinations (invented URLs, fabricated facts)
    /// </summary>
    (bool IsHallucinated, string? Details) CheckForHallucinations(string? output);
}

/// <summary>
/// Comprehensive guardrail service to enforce input/output rules and detect hallucinations.
/// </summary>
public class GuardrailService : IGuardrailService
{
    private readonly ITelemetryService _telemetry;
    private readonly ILogger<GuardrailService> _logger;
    private readonly int _minLength;
    private readonly int _maxLength;

    // Common hallucination patterns
    private static readonly Regex UrlPattern = new(@"https?://[^\s]+", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled);

    public GuardrailService(IConfiguration configuration, ITelemetryService telemetry, ILogger<GuardrailService> logger)
    {
        _telemetry = telemetry;
        _logger = logger;

        // Default values; can be overridden in configuration
        _minLength = configuration.GetValue<int?>("Guardrails:MinLength", 3) ?? 3;
        _maxLength = configuration.GetValue<int?>("Guardrails:MaxLength", 2000) ?? 2000;
    }

    public (bool IsValid, string? Error) ValidateInput(string? input, string source = "user_input")
    {
        var props = new Dictionary<string, string?>
        {
            { "source", source }
        };

        if (string.IsNullOrWhiteSpace(input))
        {
            props["reason"] = "empty";
            _telemetry.TrackEvent("guardrail_violation", props.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value!));
            return (false, "Input cannot be empty");
        }

        var length = input!.Length;
        props["length"] = length.ToString();

        if (length < _minLength)
        {
            props["reason"] = "too_short";
            props["min_length"] = _minLength.ToString();
            _telemetry.TrackEvent("guardrail_violation", props.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value!));
            return (false, $"Input is too short. Minimum length is {_minLength} characters.");
        }

        if (length > _maxLength)
        {
            props["reason"] = "too_long";
            props["max_length"] = _maxLength.ToString();
            _telemetry.TrackEvent("guardrail_violation", props.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value!));
            return (false, $"Input is too long. Maximum length is {_maxLength} characters.");
        }

        return (true, null);
    }

    public (bool IsValid, string? Error) ValidateOutput(string? output, bool expectJson = false)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            _telemetry.TrackEvent("guardrail_violation", new Dictionary<string, string>
            {
                { "type", "output_validation" },
                { "reason", "empty_output" }
            });
            return (false, "Output cannot be empty");
        }

        // If JSON is expected, validate JSON format
        if (expectJson)
        {
            try
            {
                JsonDocument.Parse(output);
            }
            catch (JsonException ex)
            {
                _telemetry.TrackEvent("guardrail_violation", new Dictionary<string, string>
                {
                    { "type", "output_validation" },
                    { "reason", "invalid_json" },
                    { "error", ex.Message }
                });
                return (false, $"Output is not valid JSON: {ex.Message}");
            }
        }

        return (true, null);
    }

    public (bool IsHallucinated, string? Details) CheckForHallucinations(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return (false, null);
        }

        var hallucinations = new List<string>();

        // Check for suspicious URLs
        var urlMatches = UrlPattern.Matches(output);
        foreach (Match match in urlMatches)
        {
            var url = match.Value;
            // Flag URLs that might be fabricated (localhost, test domains, etc.)
            if (url.Contains("localhost") || url.Contains("example.com") || url.Contains("test."))
            {
                hallucinations.Add($"Suspicious URL: {url}");
            }
        }

        // Check for suspicious email patterns (too many @ signs, malformed)
        var emailMatches = EmailPattern.Matches(output);
        if (emailMatches.Count > 5)
        {
            hallucinations.Add($"Unusually high number of emails ({emailMatches.Count})");
        }

        // Check for signs of fabricated code snippets (unmatched braces, incomplete syntax)
        if (output.Contains("```") && !IsSyntacticallyBalanced(output))
        {
            hallucinations.Add("Code snippet has unmatched braces or incomplete syntax");
        }

        if (hallucinations.Any())
        {
            _telemetry.TrackEvent("hallucination_detected", new Dictionary<string, string>
            {
                { "count", hallucinations.Count.ToString() },
                { "details", string.Join(" | ", hallucinations) }
            });

            _logger.LogWarning("[Guardrails] Hallucination detected: {Details}", string.Join(" | ", hallucinations));
            return (true, string.Join(" | ", hallucinations));
        }

        return (false, null);
    }

    private static bool IsSyntacticallyBalanced(string text)
    {
        var braces = new Stack<char>();
        foreach (var ch in text)
        {
            if (ch == '{' || ch == '[' || ch == '(')
                braces.Push(ch);
            else if (ch == '}' || ch == ']' || ch == ')')
            {
                if (braces.Count == 0)
                    return false;
                var open = braces.Pop();
                if ((ch == '}' && open != '{') || (ch == ']' && open != '[') || (ch == ')' && open != '('))
                    return false;
            }
        }
        return braces.Count == 0;
    }
}
