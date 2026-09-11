namespace Finances.Infrastructure.Identity;

/// <summary>
/// Opaque refresh token that lets a client obtain a fresh short-lived access token without
/// asking the user to log in again. Only a SHA-256 hash of the token is stored; the raw
/// value lives only on the client. Tokens are single-use: every refresh rotates to a new
/// token and revokes the old one, and logout revokes it explicitly.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    /// <summary>SHA-256 (hex) hash of the raw refresh token handed to the client.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Owner of the token (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the token was revoked (rotated or logged out). Null while still usable.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>True when the token has not been revoked and has not expired yet.</summary>
    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
