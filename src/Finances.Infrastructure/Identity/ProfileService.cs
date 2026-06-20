using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Identity;

namespace Finances.Infrastructure.Identity;

public class ProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUser _current;

    public ProfileService(UserManager<ApplicationUser> users, ICurrentUser current)
    {
        _users = users;
        _current = current;
    }

    public async Task<UserProfileDto> GetAsync(CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(_current.RequireUserId())
            ?? throw new NotFoundException("User not found.");
        return await MapAsync(user);
    }

    public async Task<UserProfileDto> UpdateAsync(UpdateProfileDto dto, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(_current.RequireUserId())
            ?? throw new NotFoundException("User not found.");

        user.FullName = dto.FullName?.Trim();
        user.Country = dto.Country?.Trim();
        user.Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "USD" : dto.Currency.Trim();
        user.MonthlyIncomeTarget = dto.MonthlyIncomeTarget;
        user.OnboardingCompleted = true;

        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        return await MapAsync(user);
    }

    private async Task<UserProfileDto> MapAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        var role = roles.Contains(AuthService.AdminRole) ? AuthService.AdminRole : AuthService.UserRole;
        return new UserProfileDto(
            user.Id, user.Email!, user.FullName, user.Country, user.Currency,
            user.MonthlyIncomeTarget, user.OnboardingCompleted, role);
    }
}
