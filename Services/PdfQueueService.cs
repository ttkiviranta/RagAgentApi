using Azure.Messaging.ServiceBus;
using RagAgentApi.Models;
using System.Text.Json;

namespace RagAgentApi.Services;

public interface IPdfQueueService
{
    Task SendPdfProcessingMessageAsync(PdfProcessingMessage message, CancellationToken cancellationToken = default);
}

public class PdfQueueService : IPdfQueueService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ILogger<PdfQueueService> _logger;

    public PdfQueueService(IConfiguration configuration, ILogger<PdfQueueService> logger)
    {
        _logger = logger;
        var connectionString = configuration["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("ServiceBus:ConnectionString is not configured");
        var queueName = configuration["ServiceBus:PdfQueueName"] ?? "pdf-processing-queue";

        _client = new ServiceBusClient(connectionString);
        _sender = _client.CreateSender(queueName);
    }

    public async Task SendPdfProcessingMessageAsync(PdfProcessingMessage message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message);
        var serviceBusMessage = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            MessageId = message.DocumentId.ToString(),
            Subject = $"pdf-processing:{message.FileName}"
        };

        await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
        _logger.LogInformation("[PdfQueueService] Message sent for document: {DocumentId}, File: {FileName}",
            message.DocumentId, message.FileName);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
