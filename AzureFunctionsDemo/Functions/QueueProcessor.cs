using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureFunctionsDemo.Functions;

public class QueueProcessor
{
    private readonly ILogger<QueueProcessor> _logger;

    public QueueProcessor(ILogger<QueueProcessor> logger)
    {
        _logger = logger;
    }

    [Function("QueueProcessor")]
    public void Run(
        [ServiceBusTrigger("demo-queue", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("QueueProcessor received message: {Message}", message);
    }
}
