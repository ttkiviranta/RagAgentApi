using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AzureFunctionsDemo.Functions;

public class PdfProcessingMessage
{
    public Guid DocumentId { get; set; }
    public Guid ThreadId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public int ChunkSize { get; set; } = 1000;
    public int ChunkOverlap { get; set; } = 200;
    public DateTime EnqueuedAt { get; set; }
}

public class PdfQueueProcessor
{
    private readonly ILogger<PdfQueueProcessor> _logger;

    public PdfQueueProcessor(ILogger<PdfQueueProcessor> logger)
    {
        _logger = logger;
    }

    [Function("PdfQueueProcessor")]
    public async Task Run(
        [ServiceBusTrigger("pdf-processing-queue", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message)
    {
        _logger.LogInformation("[PdfQueueProcessor] Received message: {MessageId}", message.MessageId);

        try
        {
            var body = message.Body.ToString();
            var pdfMessage = JsonSerializer.Deserialize<PdfProcessingMessage>(body);

            if (pdfMessage == null)
            {
                _logger.LogError("[PdfQueueProcessor] Failed to deserialize message body");
                return;
            }

            _logger.LogInformation(
                "[PdfQueueProcessor] Processing PDF: {FileName}, DocumentId: {DocumentId}, BlobUri: {BlobUri}",
                pdfMessage.FileName,
                pdfMessage.DocumentId,
                pdfMessage.BlobUri);

            // Simulate processing steps
            _logger.LogInformation("[PdfQueueProcessor] Step 1: Downloading blob {BlobName}", pdfMessage.BlobName);
            await Task.Delay(100); // placeholder for blob download

            _logger.LogInformation("[PdfQueueProcessor] Step 2: Running OCR on {FileName}", pdfMessage.FileName);
            await Task.Delay(100); // placeholder for OCR

            _logger.LogInformation("[PdfQueueProcessor] Step 3: Chunking content, ChunkSize: {ChunkSize}", pdfMessage.ChunkSize);
            await Task.Delay(100); // placeholder for chunking

            _logger.LogInformation("[PdfQueueProcessor] Step 4: Generating embeddings");
            await Task.Delay(100); // placeholder for embedding

            _logger.LogInformation("[PdfQueueProcessor] Step 5: Storing in PostgreSQL");
            await Task.Delay(100); // placeholder for storage

            _logger.LogInformation(
                "[PdfQueueProcessor] Completed processing: {FileName}, ThreadId: {ThreadId}",
                pdfMessage.FileName,
                pdfMessage.ThreadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PdfQueueProcessor] Error processing message: {MessageId}", message.MessageId);
            throw; // Re-throw so Service Bus retries the message
        }
    }
}
