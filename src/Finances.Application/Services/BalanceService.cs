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

    public async Task<BudgetHistoryDto> GetBudgetHistoryAsync(int months, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        if (months < 1) months = 1;
        if (months > 24) months = 24;

        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Ordered oldest -> newest so the grid reads as a timeline left to right.
        var monthKeys = Enumerable.Range(0, months)
            .Select(offset => currentMonthStart.AddMonths(-(months - 1 - offset)))
            .Select(d => (d.Year, d.Month))
            .ToList();

        var rangeStart = currentMonthStart.AddMonths(-(months - 1));

        var categories = await _db.Categories
            .Where(c => c.UserId == userId && c.MonthlyBudget != null && c.MonthlyBudget > 0)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var expenses = await _db.Expenses
            .Where(e => e.UserId == userId && e.Date >= rangeStart)
            .Select(e => new { e.CategoryId, e.Date.Year, e.Date.Month, e.Amount })
            .ToListAsync(ct);

        var spentLookup = expenses
            .GroupBy(e => (e.CategoryId, e.Year, e.Month))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var monthDtos = monthKeys
            .Select(k => new BudgetHistoryMonthDto(k.Year, k.Month))
            .ToList();

        var totalOver = 0;
        var categoryDtos = new List<BudgetHistoryCategoryDto>(categories.Count);

        foreach (var c in categories)
        {
            var budget = c.MonthlyBudget!.Value;
            var cells = new List<BudgetHistoryCellDto>(monthKeys.Count);
            decimal totalSpent = 0m;
            var overCount = 0;

            foreach (var (year, month) in monthKeys)
            {
                spentLookup.TryGetValue((c.Id, year, month), out var spent);
                var over = spent > budget;
                if (over)
                {
                    overCount++;
                    totalOver++;
                }
                totalSpent += spent;
                var percent = budget > 0 ? Math.Round(spent / budget * 100m, 2) : 0m;
                cells.Add(new BudgetHistoryCellDto(year, month, spent, percent, over));
            }

            categoryDtos.Add(new BudgetHistoryCategoryDto(
                c.Id, c.Name, c.Icon, c.Color, budget, totalSpent, overCount, cells));
        }

        return new BudgetHistoryDto(monthDtos, categoryDtos, totalOver);
    }
}
