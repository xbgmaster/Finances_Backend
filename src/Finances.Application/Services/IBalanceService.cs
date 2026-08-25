using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IBalanceService
{
    Task<BalanceDto> GetBalanceAsync(CancellationToken ct = default);
    Task<MonthlySummaryDto> GetMonthlyAsync(int? year, int? month, string? currency = null, CancellationToken ct = default);
    Task<BudgetHistoryDto> GetBudgetHistoryAsync(int months, string? currency = null, CancellationToken ct = default);
}
