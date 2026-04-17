using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureFunctionsDemo.Functions;

public class TimerJob
{
    private readonly ILogger<TimerJob> _logger;

    public TimerJob(ILogger<TimerJob> logger)
    {
        _logger = logger;
    }

    [Function("TimerJob")]
    public void Run([TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("TimerJob executed at: {Time}", DateTime.UtcNow);
    }
}
