using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>
/// Uploads images to an Azure Blob Storage container. The connection string comes from
/// configuration (user secrets in development, an environment variable in Docker) and is never committed.
/// </summary>
public class AzureBlobImageStorage : IImageStorage
{
    private readonly IConfiguration _configuration;
    private BlobContainerClient? _container;

    public AzureBlobImageStorage(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> UploadAsync(string folder, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        var blob = container.GetBlobClient($"{folder}/{fileName}");

        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);

        return blob.Uri.ToString();
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container != null)
            return _container;

        var connectionString = _configuration["AzureStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Azure Blob Storage is not configured (AzureStorage:ConnectionString is missing).");

        var containerName = _configuration["AzureStorage:ContainerName"] ?? "raceday-images";
        var container = new BlobContainerClient(connectionString, containerName);

        // Public read access for individual blobs, so the website can show the images with a plain <img> tag
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        _container = container;
        return container;
    }
}