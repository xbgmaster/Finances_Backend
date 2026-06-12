namespace Finances.Application.Common;

/// <summary>Datos de una imagen recibida para almacenar (sin acoplar a ASP.NET).</summary>
public record FileUpload(Stream Content, string FileName, string ContentType, long Length);

/// <summary>Almacenamiento de archivos (recibos). Implementado en Infrastructure.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(FileUpload file, string folder, CancellationToken ct = default);
    void Delete(string relativeUrl);
}
