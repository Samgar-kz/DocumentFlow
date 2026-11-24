using DocumentFlow.Application.Abstractions;
using DocumentFlow.Application.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocumentFlow.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Directory where uploaded files are kept. Mount a volume here in containers.</summary>
    public string RootPath { get; set; } = "storage";
}

/// <summary>
/// Stores files on the local file system. Swappable for blob storage (Azure Blob, S3) behind
/// <see cref="IFileStorage"/>. Every resolved path is checked to stay inside the root directory.
/// </summary>
internal sealed class LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment) : IFileStorage
{
    // Relative paths are resolved against the content root, not the process working directory.
    private readonly string _root = Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        if (!File.Exists(path))
        {
            throw new NotFoundException("File", key);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage key resolves outside the storage root.");
        }

        return path;
    }
}
