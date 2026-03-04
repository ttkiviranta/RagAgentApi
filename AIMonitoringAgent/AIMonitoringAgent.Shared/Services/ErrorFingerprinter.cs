using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AIMonitoringAgent.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AIMonitoringAgent.Shared.Services;

public interface IErrorFingerprinter
{
    ErrorFingerprint CreateFingerprint(AppInsightsException exception);
    string GenerateFingerprintHash(string exceptionType, string message, string stackTrace);
    string NormalizeStackTrace(string stackTrace);
}

public class ErrorFingerprinter : IErrorFingerprinter
{
    private readonly ILogger<ErrorFingerprinter> _logger;

    public ErrorFingerprinter(ILogger<ErrorFingerprinter> logger)
    {
        _logger = logger;
    }

    public ErrorFingerprint CreateFingerprint(AppInsightsException exception)
    {
        var normalizedStackTrace = NormalizeStackTrace(exception.StackTrace);
        var fingerprintHash = GenerateFingerprintHash(
            exception.ExceptionType,
            exception.Message,
            normalizedStackTrace);

        return new ErrorFingerprint
        {
            FingerprintHash = fingerprintHash,
            ExceptionType = exception.ExceptionType,
            Message = exception.Message,
            StackTracePattern = normalizedStackTrace,
            FirstOccurrence = exception.Timestamp,
            LastOccurrence = exception.Timestamp,
            OccurrenceCount = 1,
            AffectedOperations = new List<string> { exception.OperationName }
        };
    }

    public string GenerateFingerprintHash(string exceptionType, string message, string stackTrace)
    {
        try
        {
            var combined = $"{exceptionType}:{message}:{stackTrace}";
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return Convert.ToBase64String(hashBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate fingerprint hash");
            throw;
        }
    }

    public string NormalizeStackTrace(string stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
            return string.Empty;

        try
        {
            // Remove line numbers and specific memory addresses
            var normalized = Regex.Replace(stackTrace, @":line \d+", "");
            normalized = Regex.Replace(normalized, @"0x[0-9a-fA-F]+", "0xADDRESS");

            // Extract only the method names and file paths
            var lines = normalized.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var patterns = new List<string>();

            foreach (var line in lines)
            {
                // Extract method signature patterns
                var methodMatch = Regex.Match(line, @"at\s+(.+?)\s+in\s+");
                if (methodMatch.Success)
                {
                    patterns.Add(methodMatch.Groups[1].Value);
                }
                else if (line.Contains("at "))
                {
                    var simpleLine = Regex.Replace(line, @"\s+", " ").Trim();
                    patterns.Add(simpleLine);
                }
            }

            return string.Join("|", patterns.Take(5)); // Keep top 5 frames
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to normalize stack trace, using original");
            return stackTrace;
        }
    }
}
