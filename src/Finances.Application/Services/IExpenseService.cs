using Finances.Application.Common;
using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseDto>> GetAllAsync(int? year, int? month, string? currency, int? paymentMethodId = null, CancellationToken ct = default);
    Task<ExpenseDto> CreateAsync(ExpenseCreateDto dto, FileUpload? receipt, CancellationToken ct = default);
    Task<ExpenseDto> UpdateAsync(int id, ExpenseUpdateDto dto, FileUpload? receipt, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
