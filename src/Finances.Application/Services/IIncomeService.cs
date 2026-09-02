using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IIncomeService
{
    Task<IReadOnlyList<IncomeDto>> GetAllAsync(string? currency = null, CancellationToken ct = default);
    Task<IncomeDto> CreateAsync(IncomeCreateDto dto, CancellationToken ct = default);
    Task<IncomeDto> UpdateAsync(int id, IncomeUpdateDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
