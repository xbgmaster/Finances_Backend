using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IExpenseScheduleService
{
    Task<IReadOnlyList<ExpenseScheduleDto>> GetAllAsync(CancellationToken ct = default);
    Task<ExpenseScheduleDto> CreateAsync(ExpenseScheduleCreateDto dto, CancellationToken ct = default);
    Task<ExpenseScheduleDto> UpdateAsync(int id, ExpenseScheduleUpdateDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<int> PostDueForCurrentUserAsync(CancellationToken ct = default);
}
