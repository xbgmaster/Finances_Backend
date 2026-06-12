using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IProjectionService
{
    Task<ProjectionDto> BuildProjectionAsync(
        decimal targetSavingsRate = 0.20m,
        int historyMonths = 6,
        string lang = "en",
        CancellationToken ct = default);
}
