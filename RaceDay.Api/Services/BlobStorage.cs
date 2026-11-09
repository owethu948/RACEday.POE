using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

/// <summary>Settings bound from the "AzureBlobStorage" configuration section.</summary>
public class AzureBlobStorageOptions
{
    public const string SectionName = "AzureBlobStorage";

    /// <summary>
    /// Storage account connection string. Never commit it: supply it through user-secrets
    /// (<c>dotnet user-secrets set "AzureBlobStorage:ConnectionString" "..."</c>) or the
    /// <c>AzureBlobStorage__ConnectionString</c> environment variable.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Blob container that holds banners and profile pictures.</summary>
    public string ContainerName { get; set; } = "raceday-images";
}

/// <summary>Thrown when an upload is attempted but no storage connection string has been configured.</summary>
public class BlobStorageNotConfiguredException : InvalidOperationException
{
    public BlobStorageNotConfiguredException()
        : base("Image storage is not configured. Set AzureBlobStorage:ConnectionString for the API.") { }
}

/// <summary>Abstraction over cloud file storage so controllers (and tests) never touch the Azure SDK directly.</summary>
public interface IBlobStorageService
{
    /// <summary>Uploads an image stream and returns the public URL of the stored blob.</summary>
    /// <param name="content">The image bytes. The stream is consumed but not disposed.</param>
    /// <param name="contentType">Verified MIME type, e.g. image/png.</param>
    /// <param name="folder">Virtual folder inside the container, e.g. "banners".</param>
    /// <param name="extension">File extension including the dot, e.g. ".png".</param>
    /// <param name="cancellationToken">Token used to cancel the upload and container initialization.</param>
    Task<string> UploadImageAsync(
        Stream content,
        string contentType,
        string folder,
        string extension,
        CancellationToken cancellationToken = default);
}

/// <summary>Stores images in an Azure Blob Storage container whose blobs are publicly readable.</summary>
public class AzureBlobStorageService(
    IOptions<AzureBlobStorageOptions> options,
    ILogger<AzureBlobStorageService> logger) : IBlobStorageService
{
    private readonly AzureBlobStorageOptions _options = options.Value;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private BlobContainerClient? _container;

    public async Task<string> UploadImageAsync(
        Stream content,
        string contentType,
        string folder,
        string extension,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);

        // A random name means files can never overwrite each other and users cannot guess other URLs.
        var blobName = $"{folder}/{Guid.NewGuid():N}{extension}";
        var blob = container.GetBlobClient(blobName);

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType,
                CacheControl = "public, max-age=31536000, immutable"
            }
        }, cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Uploaded blob {BlobName} to container {Container}",
                blobName, container.Name);
        }

        return blob.Uri.ToString();
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null) return _container;

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new BlobStorageNotConfiguredException();

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_container is null)
            {
                var container = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
                // Public read access at blob level lets the browser show <img src="https://...blob.core.windows.net/...">.
                await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
                _container = container;
            }
            return _container;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
