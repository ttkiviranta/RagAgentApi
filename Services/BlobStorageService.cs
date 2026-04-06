using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace RagAgentApi.Services;

/// <summary>
/// Configuration settings for Azure Blob Storage
/// </summary>
public class BlobStorageSettings
{
    public const string SectionName = "BlobStorage";

    /// <summary>
    /// Azure Blob Storage connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container name for storing original documents
    /// </summary>
    public string ContainerName { get; set; } = "documents";

    /// <summary>
    /// Maximum file size in MB (default 100MB)
    /// </summary>
    public int MaxFileSizeMb { get; set; } = 100;

    /// <summary>
    /// Whether blob storage is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Result of a blob upload operation
/// </summary>
public class BlobUploadResult
{
    public bool Success { get; set; }
    public string? BlobUri { get; set; }
    public string? BlobName { get; set; }
    public string? ContainerName { get; set; }
    public string? FileHash { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? UploadedAt { get; set; }
}

/// <summary>
/// Result of a blob download operation
/// </summary>
public class BlobDownloadResult
{
    public bool Success { get; set; }
    public byte[]? Content { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Interface for Azure Blob Storage operations
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Upload a file to Azure Blob Storage
    /// </summary>
    Task<BlobUploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download a file from Azure Blob Storage
    /// </summary>
    Task<BlobDownloadResult> DownloadFileAsync(
        string blobUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download a file by blob name
    /// </summary>
    Task<BlobDownloadResult> DownloadFileByNameAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a blob exists
    /// </summary>
    Task<bool> BlobExistsAsync(string blobUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a blob
    /// </summary>
    Task<bool> DeleteBlobAsync(string blobUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if blob storage is enabled and configured
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Azure Blob Storage service for storing original documents
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly BlobContainerClient? _containerClient;
    private readonly BlobStorageSettings _settings;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly bool _isEnabled;

    public bool IsEnabled => _isEnabled;

    public BlobStorageService(
        IOptions<BlobStorageSettings> settings,
        ILogger<BlobStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (!_settings.Enabled || string.IsNullOrEmpty(_settings.ConnectionString))
        {
            _logger.LogWarning("[BlobStorage] Blob storage is disabled or not configured");
            _isEnabled = false;
            return;
        }

        try
        {
            _blobServiceClient = new BlobServiceClient(_settings.ConnectionString);
            _containerClient = _blobServiceClient.GetBlobContainerClient(_settings.ContainerName);
            _isEnabled = true;

            _logger.LogInformation("[BlobStorage] Initialized with container: {Container}", _settings.ContainerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BlobStorage] Failed to initialize blob storage client");
            _isEnabled = false;
        }
    }

    public async Task<BlobUploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _containerClient == null)
        {
            return new BlobUploadResult
            {
                Success = false,
                ErrorMessage = "Blob storage is not enabled or configured"
            };
        }

        try
        {
            // Ensure container exists
            await _containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);

            // Generate unique blob name with folder structure
            var sanitizedFileName = SanitizeFileName(fileName);
            var blobName = $"{documentId:N}/{sanitizedFileName}";

            _logger.LogInformation("[BlobStorage] Uploading file: {FileName} as {BlobName}", fileName, blobName);

            // Calculate file hash before upload
            fileStream.Position = 0;
            var fileHash = await ComputeSha256HashAsync(fileStream, cancellationToken);
            var fileSizeBytes = fileStream.Length;

            // Reset stream position for upload
            fileStream.Position = 0;

            // Upload blob
            var blobClient = _containerClient.GetBlobClient(blobName);
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                },
                Metadata = new Dictionary<string, string>
                {
                    { "DocumentId", documentId.ToString() },
                    { "OriginalFileName", fileName },
                    { "UploadedAt", DateTime.UtcNow.ToString("O") },
                    { "FileHash", fileHash }
                }
            };

            await blobClient.UploadAsync(fileStream, uploadOptions, cancellationToken);

            var blobUri = blobClient.Uri.ToString();

            _logger.LogInformation("[BlobStorage] Successfully uploaded {FileName} ({Size} bytes) to {BlobUri}",
                fileName, fileSizeBytes, blobUri);

            return new BlobUploadResult
            {
                Success = true,
                BlobUri = blobUri,
                BlobName = blobName,
                ContainerName = _settings.ContainerName,
                FileHash = fileHash,
                FileSizeBytes = fileSizeBytes,
                UploadedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BlobStorage] Failed to upload file: {FileName}", fileName);
            return new BlobUploadResult
            {
                Success = false,
                ErrorMessage = $"Upload failed: {ex.Message}"
            };
        }
    }

    public async Task<BlobDownloadResult> DownloadFileAsync(
        string blobUri,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _blobServiceClient == null)
        {
            return new BlobDownloadResult
            {
                Success = false,
                ErrorMessage = "Blob storage is not enabled or configured"
            };
        }

        try
        {
            var blobClient = new BlobClient(new Uri(blobUri));

            _logger.LogInformation("[BlobStorage] Downloading blob: {BlobUri}", blobUri);

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var content = response.Value.Content.ToArray();
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            // Try to get original filename from metadata
            properties.Value.Metadata.TryGetValue("OriginalFileName", out var originalFileName);

            return new BlobDownloadResult
            {
                Success = true,
                Content = content,
                ContentType = properties.Value.ContentType,
                FileName = originalFileName,
                FileSizeBytes = content.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BlobStorage] Failed to download blob: {BlobUri}", blobUri);
            return new BlobDownloadResult
            {
                Success = false,
                ErrorMessage = $"Download failed: {ex.Message}"
            };
        }
    }

    public async Task<BlobDownloadResult> DownloadFileByNameAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _containerClient == null)
        {
            return new BlobDownloadResult
            {
                Success = false,
                ErrorMessage = "Blob storage is not enabled or configured"
            };
        }

        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            return await DownloadFileAsync(blobClient.Uri.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BlobStorage] Failed to download blob by name: {BlobName}", blobName);
            return new BlobDownloadResult
            {
                Success = false,
                ErrorMessage = $"Download failed: {ex.Message}"
            };
        }
    }

    public async Task<bool> BlobExistsAsync(string blobUri, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
            return false;

        try
        {
            var blobClient = new BlobClient(new Uri(blobUri));
            var response = await blobClient.ExistsAsync(cancellationToken);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BlobStorage] Error checking blob existence: {BlobUri}", blobUri);
            return false;
        }
    }

    public async Task<bool> DeleteBlobAsync(string blobUri, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
            return false;

        try
        {
            var blobClient = new BlobClient(new Uri(blobUri));
            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            
            _logger.LogInformation("[BlobStorage] Deleted blob: {BlobUri}, Result: {Deleted}", blobUri, response.Value);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BlobStorage] Failed to delete blob: {BlobUri}", blobUri);
            return false;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Select(c => invalidChars.Contains(c) ? '_' : c)
            .ToArray());
        return sanitized;
    }

    private static async Task<string> ComputeSha256HashAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
