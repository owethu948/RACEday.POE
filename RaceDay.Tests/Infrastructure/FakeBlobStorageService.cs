using RaceDay.Api.Services;

namespace RaceDay.Tests.Infrastructure;

/// <summary>Stands in for Azure Blob Storage in tests and remembers every upload it was asked to make.</summary>
public sealed class FakeBlobStorageService : IBlobStorageService
{
    public sealed record UploadedImage(string ContentType, string Folder, string Extension, long Length);

    private readonly List<UploadedImage> _uploads = new();
    private readonly object _lock = new();

    public IReadOnlyList<UploadedImage> Uploads
    {
        get { lock (_lock) return _uploads.ToList(); }
    }

    public async Task<string> UploadImageAsync(Stream content, string contentType, string folder, string extension, CancellationToken cancellationToken = default)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);

        lock (_lock) _uploads.Add(new UploadedImage(contentType, folder, extension, copy.Length));
        return $"https://fakeaccount.blob.core.windows.net/raceday-images/{folder}/{Guid.NewGuid():N}{extension}";
    }
}
