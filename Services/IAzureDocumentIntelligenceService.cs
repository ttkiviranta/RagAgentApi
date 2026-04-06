namespace RagAgentApi.Services;

/// <summary>
/// Interface for Azure Document Intelligence (Form Recognizer) service
/// </summary>
public interface IAzureDocumentIntelligenceService
{
    /// <summary>
    /// Extract text from a document stream (PDF, images, etc.)
    /// </summary>
    Task<string> ExtractTextAsync(Stream documentStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract text from a document URL
    /// </summary>
    Task<string> ExtractTextFromUrlAsync(string documentUrl, CancellationToken cancellationToken = default);
}
