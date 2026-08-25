using Finances.Application.Common;
using Finances.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class BalanceService : IBalanceService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public BalanceService(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
    }

    public async Task<BalanceDto> GetBalanceAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;

        var incomes = await _db.Incomes
            .Where(i => i.UserId == userId)
            .Select(i => new { i.Amount, i.Currency })
            .ToListAsync(ct);
        var expenses = await _db.Expenses
            .Where(e => e.UserId == userId)
            .Select(e => new { e.Amount, e.Currency })
            .ToListAsync(ct);
        var exchanges = await _db.CurrencyExchanges
            .Where(x => x.UserId == userId)
            .Select(x => new { x.FromCurrency, x.FromAmount, x.ToCurrency, x.ToAmount })
            .ToListAsync(ct);

        // Group totals per currency (null currency = base currency). Exchanges move money between
        // currencies without any conversion of the reported totals: they only reduce the source
        // balance and increase the destination balance.
        var perCurrency = new Dictionary<string, (decimal Income, decimal Expense, decimal ExchangeNet)>(StringComparer.OrdinalIgnoreCase);

        foreach (var i in incomes)
        {
            var cur = string.IsNullOrWhiteSpace(i.Currency) ? baseCurrency : i.Currency!;
            var acc = perCurrency.GetValueOrDefault(cur);
            perCurrency[cur] = (acc.Income + i.Amount, acc.Expense, acc.ExchangeNet);
        }
        foreach (var e in expenses)
        {
            var cur = string.IsNullOrWhiteSpace(e.Currency) ? baseCurrency : e.Currency!;
            var acc = perCurrency.GetValueOrDefault(cur);
            perCurrency[cur] = (acc.Income, acc.Expense + e.Amount, acc.ExchangeNet);
        }
        foreach (var x in exchanges)
        {
            var fromAcc = perCurrency.GetValueOrDefault(x.FromCurrency);
            perCurrency[x.FromCurrency] = (fromAcc.Income, fromAcc.Expense, fromAcc.ExchangeNet - x.FromAmount);
            var toAcc = perCurrency.GetValueOrDefault(x.ToCurrency);
            perCurrency[x.ToCurrency] = (toAcc.Income, toAcc.Expense, toAcc.ExchangeNet + x.ToAmount);
        }

        // Base currency always appears first (even if it has no movements yet).
        var byCurrency = perCurrency
            .Select(kv => new CurrencyBalanceDto(
                kv.Key, kv.Value.Income, kv.Value.Expense,
                kv.Value.Income - kv.Value.Expense + kv.Value.ExchangeNet))
            .OrderByDescending(c => string.Equals(c.Currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.Currency)
            .ToList();

        var baseEntry = byCurrency.FirstOrDefault(c => string.Equals(c.Currency, baseCurrency, StringComparison.OrdinalIgnoreCase));
        var totalIncome = baseEntry?.TotalIncome ?? 0m;
        var totalExpense = baseEntry?.TotalExpense ?? 0m;
        var baseBalance = baseEntry?.Balance ?? 0m;

        return new BalanceDto(totalIncome, totalExpense, baseBalance, baseCurrency, byCurrency);
    }

    public async Task<MonthlySummaryDto> GetMonthlyAsync(int? year, int? month, string? currency = null, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var target = string.IsNullOrWhiteSpace(currency) ? baseCurrency : currency.Trim().ToUpperInvariant();
        var isBase = target == baseCurrency.ToUpperInvariant();
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        // The monthly summary is scoped to a single currency (the lens). Budgets are read for
        // that same currency, so spend and budget are always comparable. A null stored currency
        // means "base currency".
        var incomeQuery = _db.Incomes
            .Where(i => i.UserId == userId && i.Date.Year == y && i.Date.Month == m);
        incomeQuery = isBase
            ? incomeQuery.Where(i => i.Currency == null || i.Currency == target)
            : incomeQuery.Where(i => i.Currency == target);
        var income = await incomeQuery.SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;

        var expenseQuery = _db.Expenses
            .Include(e => e.Category)
            .Where(e => e.UserId == userId && e.Date.Year == y && e.Date.Month == m);
        expenseQuery = isBase
            ? expenseQuery.Where(e => e.Currency == null || e.Currency == target)
            : expenseQuery.Where(e => e.Currency == target);
        var monthExpenses = await expenseQuery.ToListAsync(ct);

        var expense = monthExpenses.Sum(e => e.Amount);

        var byCategory = monthExpenses
            .GroupBy(e => e.Category!)
            .Select(g => new CategorySpendDto(
                g.Key.Id, g.Key.Name, g.Key.Icon, g.Key.Color,
                g.Sum(e => e.Amount), CategoryBudgetHelper.Effective(g.Key, target, baseCurrency)))
            .OrderByDescending(c => c.Spent)
            .ToList();

        return new MonthlySummaryDto(y, m, income, expense, income - expense, byCategory);
    }

    public async Task<BudgetHistoryDto> GetBudgetHistoryAsync(int months, string? currency = null, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var target = string.IsNullOrWhiteSpace(currency) ? baseCurrency : currency.Trim().ToUpperInvariant();
        var isBase = target == baseCurrency.ToUpperInvariant();
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

        // Only categories that have a budget in the selected currency.
        var allCategories = await _db.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        var categories = allCategories
            .Where(c => CategoryBudgetHelper.Effective(c, target, baseCurrency) is decimal b && b > 0)
            .ToList();

        var expenseQuery = _db.Expenses
            .Where(e => e.UserId == userId && e.Date >= rangeStart);
        expenseQuery = isBase
            ? expenseQuery.Where(e => e.Currency == null || e.Currency == target)
            : expenseQuery.Where(e => e.Currency == target);
        var expenses = await expenseQuery
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
            var budget = CategoryBudgetHelper.Effective(c, target, baseCurrency)!.Value;
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
