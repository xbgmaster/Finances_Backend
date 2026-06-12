using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IUserAdminService
{
    Task<PagedResult<AdminUserDto>> GetUsersAsync(UserFilter filter, CancellationToken ct = default);
    Task<AdminUserDto> GetUserAsync(string id, CancellationToken ct = default);
    Task<AdminStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<byte[]> ExportUsersCsvAsync(UserFilter filter, CancellationToken ct = default);
}
