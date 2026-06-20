using System.Security.Cryptography;
using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Application.Services;
using Finances.Infrastructure.Persistence;
using Finances.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Finances.Infrastructure.Identity;

public class AuthService : IAuthService
{
    public const string UserRole = "User";
    public const string AdminRole = "Admin";

    private readonly UserManager<ApplicationUser> _users;
    private readonly IJwtTokenGenerator _jwt;
    private readonly FinanceDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> users,
        IJwtTokenGenerator jwt,
        FinanceDbContext db,
        IEmailSender email,
        ILogger<AuthService> logger)
    {
        _users = users;
        _jwt = jwt;
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        var existing = await _users.FindByEmailAsync(dto.Email);
        if (existing is not null)
            throw new ConflictException("An account with this email already exist.");

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _users.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await _users.AddToRoleAsync(user, UserRole);

        // Cada usuario nuevo recibe las categorias por defecto.
        _db.Categories.AddRange(DefaultCategories.For(user.Id));
        await _db.SaveChangesAsync(ct);

        return await BuildResultAsync(user);
    }

    public async Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(dto.Email);
        if (user is null || !await _users.CheckPasswordAsync(user, dto.Password))
            throw new ValidationException("Incorrect email or password.");

        return await BuildResultAsync(user);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(dto.Email);
        // No revelar si el correo existe: si no existe, terminamos en silencio.
        if (user is null)
        {
            _logger.LogInformation("Forgot-password solicitado para correo inexistente: {Email}", dto.Email);
            return;
        }

        var tempPassword = GenerateTempPassword();
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, tempPassword);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        var subject = "Tu contrasena temporal / Your temporary password";
        var body = $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:auto"">
  <h2>Recuperacion de contrasena</h2>
  <p>Hola{(string.IsNullOrWhiteSpace(user.FullName) ? "" : $" {user.FullName}")},</p>
  <p>Tu contrasena temporal es:</p>
  <p style=""font-size:20px;font-weight:bold;letter-spacing:1px;background:#f1f5f9;padding:12px 16px;border-radius:8px;display:inline-block"">{tempPassword}</p>
  <p>Inicia sesion con ella y cambiala lo antes posible desde tu perfil.</p>
  <hr/>
  <p style=""color:#64748b;font-size:13px"">Si no solicitaste este cambio, ignora este correo.</p>
</div>";

        await _email.SendAsync(user.Email!, subject, body, ct);
        _logger.LogInformation("Contrasena temporal generada y enviada para {Email}.", user.Email);
    }

    /// <summary>
    /// Genera una contrasena temporal segura que cumple la politica de Identity
    /// (minusculas + mayusculas + digito, longitud 12).
    /// </summary>
    private static string GenerateTempPassword()
    {
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string all = lower + upper + digits;

        Span<char> chars = stackalloc char[12];
        chars[0] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[1] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < chars.Length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Mezcla para no dejar el patron fijo al inicio.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    private async Task<AuthResultDto> BuildResultAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        var token = _jwt.Generate(user.Id, user.Email!, roles);
        var role = roles.Contains(AdminRole) ? AdminRole : UserRole;
        var info = new UserInfoDto(user.Id, user.Email!, user.FullName, role, user.OnboardingCompleted);
        return new AuthResultDto(token.Token, token.ExpiresAt, info);
    }
}
