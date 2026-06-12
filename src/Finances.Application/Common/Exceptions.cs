namespace Finances.Application.Common;

/// <summary>Recurso no encontrado (se mapea a HTTP 404).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Conflicto con el estado actual (se mapea a HTTP 409).</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Datos de entrada invalidos (se mapea a HTTP 400).</summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
