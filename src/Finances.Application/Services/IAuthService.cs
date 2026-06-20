using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default);

    /// <summary>
    /// Si el correo existe, genera una contrasena temporal, la asigna a la cuenta
    /// y la envia por email. No revela si el correo existe o no.
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default);
}
