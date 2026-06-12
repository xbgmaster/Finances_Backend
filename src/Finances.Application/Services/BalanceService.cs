using Finances.Application.Common;
using Finances.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class BalanceService : IBalanceService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public BalanceService(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<BalanceDto> GetBalanceAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var totalIncome = await _db.Incomes.Where(i => i.UserId == userId).SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
        var totalExpense = await _db.Expenses.Where(e => e.UserId == userId).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        return new BalanceDto(totalIncome, totalExpense, totalIncome - totalExpense);
    }

    public async Task<MonthlySummaryDto> GetMonthlyAsync(int? year, int? month, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        var income = await _db.Incomes
            .Where(i => i.UserId == userId && i.Date.Year == y && i.Date.Month == m)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;

        var monthExpenses = await _db.Expenses
            .Include(e => e.Category)
            .Where(e => e.UserId == userId && e.Date.Year == y && e.Date.Month == m)
            .ToListAsync(ct);

        var expense = monthExpenses.Sum(e => e.Amount);

        var byCategory = monthExpenses
            .GroupBy(e => e.Category!)
            .Select(g => new CategorySpendDto(
                g.Key.Id, g.Key.Name, g.Key.Icon, g.Key.Color,
                g.Sum(e => e.Amount), g.Key.MonthlyBudget))
            .OrderByDescending(c => c.Spent)
            .ToList();

        return new MonthlySummaryDto(y, m, income, expense, income - expense, byCategory);
    }
}
