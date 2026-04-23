using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RagAgentApi.Services;

namespace RagAgentApi.Middleware;

/// <summary>
/// Middleware that performs lightweight input validation (guardrails) for RAG endpoints.
/// Rejects empty / too short / too long inputs and logs violations via TelemetryService.
/// </summary>
public class GuardrailMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GuardrailMiddleware> _logger;

    public GuardrailMiddleware(RequestDelegate next, ILogger<GuardrailMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IGuardrailService guardrail, ITelemetryService telemetry)
    {
        try
        {
            // Only inspect POST JSON requests to /api/rag/*
            if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                context.Request.Path.StartsWithSegments("/api/rag") &&
                context.Request.ContentType != null &&
                context.Request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var root = doc.RootElement;

                        // Fields to validate: query, url, content
                        string? candidate = null;
                        if (root.TryGetProperty("query", out var q))
                            candidate = q.GetString();
                        else if (root.TryGetProperty("url", out var u))
                            candidate = u.GetString();
                        else if (root.TryGetProperty("content", out var c))
                            candidate = c.GetString();

                        if (candidate != null)
                        {
                            var (isValid, error) = guardrail.ValidateInput(candidate, "api_request");
                            if (!isValid)
                            {
                                telemetry?.TrackEvent("guardrail_violation", new Dictionary<string, string> { { "reason", error ?? "invalid" }, { "path", context.Request.Path } });
                                _logger.LogInformation("[Guardrail] Rejected request to {Path}: {Reason}", context.Request.Path, error);
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                context.Response.ContentType = "application/json";
                                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = error }));
                                return;
                            }
                        }
                    }
                    catch (JsonException jex)
                    {
                        _logger.LogDebug(jex, "[Guardrail] Failed to parse JSON body for validation");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Guardrail] Exception during input validation - allowing request to proceed");
        }

        await _next(context);
    }
}
