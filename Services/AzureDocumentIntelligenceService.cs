using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

namespace RagAgentApi.Services;

/// <summary>
/// Azure Document Intelligence service implementation for text extraction from documents
/// </summary>
public class AzureDocumentIntelligenceService : IAzureDocumentIntelligenceService
{
    private readonly DocumentAnalysisClient? _client;
    private readonly ILogger<AzureDocumentIntelligenceService> _logger;
    private readonly bool _isConfigured;

    public AzureDocumentIntelligenceService(
        IConfiguration configuration, 
        ILogger<AzureDocumentIntelligenceService> logger)
    {
        _logger = logger;
        
        var endpoint = configuration["Azure:DocumentIntelligence:Endpoint"];
        var key = configuration["Azure:DocumentIntelligence:Key"];

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(key))
        {
            try
            {
                _client = new DocumentAnalysisClient(
                    new Uri(endpoint), 
                    new AzureKeyCredential(key));
                _isConfigured = true;
                _logger.LogInformation("[DocumentIntelligence] Service configured with endpoint: {Endpoint}", endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DocumentIntelligence] Failed to initialize client");
                _isConfigured = false;
            }
        }
        else
        {
            _logger.LogWarning("[DocumentIntelligence] Service not configured - missing endpoint or key");
            _isConfigured = false;
        }
    }

    /// <inheritdoc/>
    public async Task<string> ExtractTextAsync(Stream documentStream, CancellationToken cancellationToken = default)
    {
        if (!_isConfigured || _client == null)
        {
            _logger.LogWarning("[DocumentIntelligence] Service not configured, returning empty");
            return string.Empty;
        }

        try
        {
            _logger.LogDebug("[DocumentIntelligence] Starting document analysis from stream ({Length} bytes)", 
                documentStream.Length);

            // Use prebuilt-read model for general text extraction (best for OCR)
            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-read",
                documentStream,
                cancellationToken: cancellationToken);

            var result = operation.Value;

            if (result == null || result.Content == null)
            {
                _logger.LogWarning("[DocumentIntelligence] Analysis returned no content");
                return string.Empty;
            }

            _logger.LogInformation("[DocumentIntelligence] Extracted {Pages} pages, {Length} characters", 
                result.Pages.Count, result.Content.Length);

            return result.Content;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "[DocumentIntelligence] Azure API error: {Status} - {Message}", 
                ex.Status, ex.Message);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentIntelligence] Unexpected error during text extraction");
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public async Task<string> ExtractTextFromUrlAsync(string documentUrl, CancellationToken cancellationToken = default)
    {
        if (!_isConfigured || _client == null)
        {
            _logger.LogWarning("[DocumentIntelligence] Service not configured, returning empty");
            return string.Empty;
        }

        try
        {
            _logger.LogDebug("[DocumentIntelligence] Starting document analysis from URL: {Url}", documentUrl);

            var operation = await _client.AnalyzeDocumentFromUriAsync(
                WaitUntil.Completed,
                "prebuilt-read",
                new Uri(documentUrl),
                cancellationToken: cancellationToken);

            var result = operation.Value;

            if (result == null || result.Content == null)
            {
                _logger.LogWarning("[DocumentIntelligence] Analysis returned no content");
                return string.Empty;
            }

            _logger.LogInformation("[DocumentIntelligence] Extracted {Pages} pages, {Length} characters from URL", 
                result.Pages.Count, result.Content.Length);

            return result.Content;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "[DocumentIntelligence] Azure API error: {Status} - {Message}", 
                ex.Status, ex.Message);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DocumentIntelligence] Unexpected error during URL text extraction");
            return string.Empty;
        }
    }
}
