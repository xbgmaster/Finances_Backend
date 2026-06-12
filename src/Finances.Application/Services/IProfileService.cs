using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IProfileService
{
    Task<UserProfileDto> GetAsync(CancellationToken ct = default);
    Task<UserProfileDto> UpdateAsync(UpdateProfileDto dto, CancellationToken ct = default);
}
