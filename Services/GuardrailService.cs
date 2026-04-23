using Microsoft.Extensions.Configuration;

namespace RagAgentApi.Services;

public interface IGuardrailService
{
    /// <summary>
    /// Validate input and return (isValid, errorMessage)
    /// </summary>
    (bool IsValid, string? Error) ValidateInput(string? input, string source = "user_input");
}

/// <summary>
/// Lightweight guardrail service to enforce basic input rules and report violations via telemetry.
/// </summary>
public class GuardrailService : IGuardrailService
{
    private readonly ITelemetryService _telemetry;
    private readonly int _minLength;
    private readonly int _maxLength;

    public GuardrailService(IConfiguration configuration, ITelemetryService telemetry)
    {
        _telemetry = telemetry;

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
}
