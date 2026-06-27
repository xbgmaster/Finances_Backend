using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default);

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
