using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IUserAdminService
{
    Task<PagedResult<AdminUserDto>> GetUsersAsync(UserFilter filter, CancellationToken ct = default);
    Task<AdminUserDto> GetUserAsync(string id, CancellationToken ct = default);
    Task<AdminStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<byte[]> ExportUsersCsvAsync(UserFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes a user and every piece of data they own (expenses, incomes,
    /// credits, payment methods, exchanges, categories, sessions and receipt files).
    /// Guards against deleting yourself or another administrator.
    /// </summary>
    Task DeleteUserAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Sets the per-user feature blocklist (the modules the user is NOT allowed to see).
    /// Front-end hides the matching menu items and routes; enables future subscription tiers.
    /// </summary>
    Task<AdminUserDto> SetUserFeaturesAsync(string id, IReadOnlyList<string> disabledFeatures, CancellationToken ct = default);
}
