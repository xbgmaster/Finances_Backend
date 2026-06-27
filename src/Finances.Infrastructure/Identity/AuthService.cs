using System.Net;
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
    private readonly AppUrls _urls;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> users,
        IJwtTokenGenerator jwt,
        FinanceDbContext db,
        IEmailSender email,
        AppUrls urls,
        ILogger<AuthService> logger)
    {
        _users = users;
        _jwt = jwt;
        _db = db;
        _email = email;
        _urls = urls;
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

        // Every new user receives the default categories.
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
        // Do not reveal whether the email exists: if it does not, finish silently.
        if (user is null)
        {
            _logger.LogInformation("Forgot-password requested for unknown email: {Email}", dto.Email);
            return;
        }

        // Generate a single-use reset token and build a link to the front-end page.
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var link = $"{_urls.FrontendBaseUrl.TrimEnd('/')}/restore-password" +
                   $"?email={WebUtility.UrlEncode(user.Email)}&token={WebUtility.UrlEncode(token)}";

        var subject = "Reset your password";
        var body = $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:auto"">
  <h2>Password reset</h2>
  <p>Hi{(string.IsNullOrWhiteSpace(user.FullName) ? "" : $" {user.FullName}")},</p>
  <p>We received a request to reset your password. Click the button below to choose a new one:</p>
  <p style=""text-align:center;margin:28px 0"">
    <a href=""{link}"" style=""background:#6366f1;color:#fff;text-decoration:none;padding:12px 22px;border-radius:10px;font-weight:bold;display:inline-block"">Reset password</a>
  </p>
  <p style=""color:#64748b;font-size:13px"">Or copy and paste this link into your browser:</p>
  <p style=""word-break:break-all;font-size:13px"">{link}</p>
  <hr/>
  <p style=""color:#64748b;font-size:13px"">This link expires soon. If you did not request this, you can safely ignore this email.</p>
</div>";

        await _email.SendAsync(user.Email!, subject, body, ct);
        _logger.LogInformation("Password reset link generated and sent to {Email}.", user.Email);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(dto.Email);
        // Use a generic error so we don't reveal whether the email exists.
        if (user is null)
            throw new ValidationException("Invalid or expired reset link.");

        var result = await _users.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
        {
            // Token errors are reported generically; password-policy errors are surfaced.
            var isTokenError = result.Errors.Any(e => e.Code.Contains("Token", StringComparison.OrdinalIgnoreCase));
            throw new ValidationException(isTokenError
                ? "Invalid or expired reset link."
                : string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        _logger.LogInformation("Password successfully reset for {Email}.", user.Email);
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
