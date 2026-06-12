using Finances.Application.Common;

namespace Finances.Infrastructure.Storage;

public class FileStorageOptions
{
    /// <summary>Carpeta fisica raiz desde la que se sirven los archivos (wwwroot).</summary>
    public string RootPath { get; set; } = string.Empty;
}

/// <summary>Guarda los archivos en disco bajo la raiz publica (wwwroot/uploads).</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;

    public LocalFileStorage(FileStorageOptions options) => _options = options;

    public async Task<string> SaveAsync(FileUpload file, string folder, CancellationToken ct = default)
    {
        var targetDir = Path.Combine(_options.RootPath, folder);
        Directory.CreateDirectory(targetDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(targetDir, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.Content.CopyToAsync(stream, ct);
        }

        return $"/{folder}/{fileName}";
    }

    public void Delete(string relativeUrl)
    {
        if (string.IsNullOrEmpty(relativeUrl)) return;
        var filePath = Path.Combine(
            _options.RootPath,
            relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
