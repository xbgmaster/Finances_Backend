using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default);

    /// <summary>
    /// Exchanges a valid, non-revoked refresh token for a fresh access token and a new
    /// (rotated) refresh token. Throws when the token is missing, expired or revoked.
    /// </summary>
    Task<AuthResultDto> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes a refresh token (sign out). Safe to call with an unknown token.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// If the email exists, generates a password reset token and emails a reset
    /// link to the user. Does not reveal whether the email exists.
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default);

    /// <summary>
    /// Validates the reset token and sets the user's new password.
    /// </summary>
    Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);
}
