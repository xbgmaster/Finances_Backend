namespace Finances.Application.Common;

/// <summary>Usuario autenticado actual, derivado de los claims del JWT.</summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }

    /// <summary>Devuelve el UserId o lanza si no hay usuario autenticado.</summary>
    string RequireUserId();
}
