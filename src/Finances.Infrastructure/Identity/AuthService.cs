using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Application.Services;
using Finances.Infrastructure.Persistence;
using Finances.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;

namespace Finances.Infrastructure.Identity;

public class AuthService : IAuthService
{
    public const string UserRole = "User";
    public const string AdminRole = "Admin";

    private readonly UserManager<ApplicationUser> _users;
    private readonly IJwtTokenGenerator _jwt;
    private readonly FinanceDbContext _db;

    public AuthService(UserManager<ApplicationUser> users, IJwtTokenGenerator jwt, FinanceDbContext db)
    {
        _users = users;
        _jwt = jwt;
        _db = db;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        var existing = await _users.FindByEmailAsync(dto.Email);
        if (existing is not null)
            throw new ConflictException("Ya existe una cuenta con ese correo.");

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
            throw new ValidationException("Correo o contrasena incorrectos.");

        return await BuildResultAsync(user);
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
