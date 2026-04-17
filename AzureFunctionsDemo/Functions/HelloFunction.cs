using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AzureFunctionsDemo.Functions;

public class HelloFunction
{
    private readonly ILogger<HelloFunction> _logger;

    public HelloFunction(ILogger<HelloFunction> logger)
    {
        _logger = logger;
    }

    [Function("HelloFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("HelloFunction triggered.");

        string name = req.Query["name"] ?? "World";

        if (req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                if (data != null && data.TryGetValue("name", out var jsonName))
                    name = jsonName;
            }
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        await response.WriteStringAsync($"Hello, {name}!");

        return response;
    }
}
