using RaceDay.Api.Services;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>Stands in for Azure Blob Storage in tests: nothing is uploaded, a predictable URL comes back.</summary>
public class FakeImageStorage : IImageStorage
{
    public Task<string> UploadAsync(string folder, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake-storage.test/{folder}/{fileName}");
}