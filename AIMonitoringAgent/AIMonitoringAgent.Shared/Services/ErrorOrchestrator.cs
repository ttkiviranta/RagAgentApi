using AIMonitoringAgent.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AIMonitoringAgent.Shared.Services;

public interface IErrorOrchestrator
{
    Task<AnalysisResult> ProcessErrorAsync(
        AppInsightsException exception,
        List<NotificationConfig>? notificationConfigs = null);
}

public class ErrorOrchestrator : IErrorOrchestrator
{
    private readonly IErrorParser _errorParser;
    private readonly IErrorFingerprinter _fingerprinter;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorMemoryStore _vectorMemory;
    private readonly ILlmAnalyzer _llmAnalyzer;
    private readonly IDeploymentCorrelator _deploymentCorrelator;
    private readonly INotificationRouter _notificationRouter;
    private readonly ILogger<ErrorOrchestrator> _logger;

    public ErrorOrchestrator(
        IErrorParser errorParser,
        IErrorFingerprinter fingerprinter,
        IEmbeddingService embeddingService,
        IVectorMemoryStore vectorMemory,
        ILlmAnalyzer llmAnalyzer,
        IDeploymentCorrelator deploymentCorrelator,
        INotificationRouter notificationRouter,
        ILogger<ErrorOrchestrator> logger)
    {
        _errorParser = errorParser;
        _fingerprinter = fingerprinter;
        _embeddingService = embeddingService;
        _vectorMemory = vectorMemory;
        _llmAnalyzer = llmAnalyzer;
        _deploymentCorrelator = deploymentCorrelator;
        _notificationRouter = notificationRouter;
        _logger = logger;
    }

    public async Task<AnalysisResult> ProcessErrorAsync(
        AppInsightsException exception,
        List<NotificationConfig>? notificationConfigs = null)
    {
        try
        {
            _logger.LogInformation(
                "Processing error: {ExceptionType} - {Message}",
                exception.ExceptionType,
                exception.Message);

            // Step 1: Create fingerprint
            var fingerprint = _fingerprinter.CreateFingerprint(exception);
            _logger.LogDebug("Created fingerprint: {Hash}", fingerprint.FingerprintHash);

            // Step 2: Check if error already exists
            var existingError = await _vectorMemory.GetErrorByFingerprintAsync(fingerprint.FingerprintHash);

            // Step 3: Generate embedding
            var combinedText = $"{exception.ExceptionType} {exception.Message} {fingerprint.StackTracePattern}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(combinedText);

            // Step 4: Search for similar errors
            var similarErrors = await _vectorMemory.SearchSimilarErrorsAsync(embedding, topK: 5);
            _logger.LogInformation("Found {Count} similar errors", similarErrors.Count);

            // Step 5: Correlate with deployment
            var deploymentCorrelation = await _deploymentCorrelator.CorrelateDeploymentAsync(
                exception.Timestamp,
                exception.OperationName);

            // Step 6: Run LLM analysis
            var analysis = await _llmAnalyzer.AnalyzeErrorAsync(
                exception,
                similarErrors,
                deploymentCorrelation);

            // Step 7: Store in vector memory
            var vectorRecord = new VectorMemoryRecord
            {
                Id = fingerprint.FingerprintHash,
                FingerprintHash = fingerprint.FingerprintHash,
                ExceptionType = exception.ExceptionType,
                Message = exception.Message,
                StackTracePattern = fingerprint.StackTracePattern,
                RootCauseAnalysis = analysis.RootCauseAnalysis,
                Severity = analysis.Severity,
                Category = analysis.Category,
                Timestamp = exception.Timestamp,
                OccurrenceCount = existingError?.OccurrenceCount + 1 ?? 1,
                AffectedOperations = analysis.AffectedOperations,
                RecommendedActions = analysis.RecommendedActions,
                DeploymentId = deploymentCorrelation?.DeploymentId
            };

            await _vectorMemory.StoreErrorAsync(vectorRecord, embedding);

            // Step 8: Send notifications
            if (notificationConfigs?.Any(c => c.Enabled) == true)
            {
                await _notificationRouter.RouteNotificationAsync(analysis, exception, notificationConfigs);
            }

            _logger.LogInformation("Successfully processed error {ErrorId}", analysis.ErrorId);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process error");
            throw;
        }
    }
}
