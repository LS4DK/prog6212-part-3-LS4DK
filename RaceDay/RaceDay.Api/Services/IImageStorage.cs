namespace RaceDay.Api.Services;

/// <summary>Stores an uploaded image somewhere public and returns its URL.</summary>
public interface IImageStorage
{
    Task<string> UploadAsync(string folder, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);
}