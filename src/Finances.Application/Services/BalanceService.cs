using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
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

        // Available balance is the cash you can spend: the sum of debit and cash accounts
        // in that currency. A charge on a credit card is debt, not cash. A movement with no
        // account is not part of any balance. This matches the Balance shown on each account.
        var wallets = await _db.PaymentMethods
            .Where(p => p.UserId == userId && !p.Archived && p.Type != PaymentMethodType.CreditCard)
            .Select(p => new { p.Id, Currency = p.Currency ?? baseCurrency })
            .ToListAsync(ct);
        var walletIds = wallets.Select(w => w.Id).ToList();
        var currencyById = wallets.ToDictionary(
            w => w.Id,
            w => w.Currency.ToUpperInvariant());

        var incomes = await _db.Incomes
            .Where(i => i.UserId == userId && i.PaymentMethodId != null && walletIds.Contains(i.PaymentMethodId.Value))
            .Select(i => new { i.Amount, i.Currency, AccountId = i.PaymentMethodId!.Value })
            .ToListAsync(ct);
        var expenses = await _db.Expenses
            .Where(e => e.UserId == userId && e.PaymentMethodId != null && walletIds.Contains(e.PaymentMethodId.Value))
            .Select(e => new { e.Amount, e.Currency, AccountId = e.PaymentMethodId!.Value })
            .ToListAsync(ct);
        var exchanges = await _db.CurrencyExchanges
            .Where(x => x.UserId == userId
                && ((x.FromPaymentMethodId != null && walletIds.Contains(x.FromPaymentMethodId.Value))
                    || (x.ToPaymentMethodId != null && walletIds.Contains(x.ToPaymentMethodId.Value))))
            .Select(x => new { x.FromPaymentMethodId, x.FromCurrency, x.FromAmount, x.ToPaymentMethodId, x.ToCurrency, x.ToAmount })
            .ToListAsync(ct);
        var cardPayments = await _db.CardPayments
            .Where(p => p.UserId == userId && p.SourcePaymentMethodId != null
                && walletIds.Contains(p.SourcePaymentMethodId.Value))
            .Select(p => new { p.Amount, p.Currency, AccountId = p.SourcePaymentMethodId!.Value })
            .ToListAsync(ct);

        var perCurrency = new Dictionary<string, (decimal Income, decimal Expense, decimal ExchangeNet)>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in wallets)
            perCurrency.TryAdd(w.Currency.ToUpperInvariant(), (0m, 0m, 0m));

        bool SameAccountCurrency(int accountId, string? movementCurrency)
        {
            if (!currencyById.TryGetValue(accountId, out var accountCurrency)) return false;
            var movement = string.IsNullOrWhiteSpace(movementCurrency) ? baseCurrency : movementCurrency;
            return string.Equals(accountCurrency, movement, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var i in incomes)
        {
            if (!SameAccountCurrency(i.AccountId, i.Currency)) continue;
            var cur = currencyById[i.AccountId];
            var acc = perCurrency.GetValueOrDefault(cur);
            perCurrency[cur] = (acc.Income + i.Amount, acc.Expense, acc.ExchangeNet);
        }
        foreach (var e in expenses)
        {
            if (!SameAccountCurrency(e.AccountId, e.Currency)) continue;
            var cur = currencyById[e.AccountId];
            var acc = perCurrency.GetValueOrDefault(cur);
            perCurrency[cur] = (acc.Income, acc.Expense + e.Amount, acc.ExchangeNet);
        }
        foreach (var p in cardPayments)
        {
            if (!currencyById.TryGetValue(p.AccountId, out var cur)) continue;
            var acc = perCurrency.GetValueOrDefault(cur);
            perCurrency[cur] = (acc.Income, acc.Expense + p.Amount, acc.ExchangeNet);
        }
        foreach (var x in exchanges)
        {
            if (x.FromPaymentMethodId is int fromId
                && currencyById.TryGetValue(fromId, out var fromCur)
                && string.Equals(x.FromCurrency, fromCur, StringComparison.OrdinalIgnoreCase))
            {
                var acc = perCurrency.GetValueOrDefault(fromCur);
                perCurrency[fromCur] = (acc.Income, acc.Expense, acc.ExchangeNet - x.FromAmount);
            }
            if (x.ToPaymentMethodId is int toId
                && currencyById.TryGetValue(toId, out var toCur)
                && string.Equals(x.ToCurrency, toCur, StringComparison.OrdinalIgnoreCase))
            {
                var acc = perCurrency.GetValueOrDefault(toCur);
                perCurrency[toCur] = (acc.Income, acc.Expense, acc.ExchangeNet + x.ToAmount);
            }
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

        // Only categories that have a budget in the selected currency, or that
        // actually spent something in the requested window (so unbudgeted
        // spending like entertainment or debt payments still shows up).
        var allCategories = await _db.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

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
        var spentCategoryIds = expenses.Select(e => e.CategoryId).ToHashSet();

        var categories = allCategories
            .Where(c =>
                (CategoryBudgetHelper.Effective(c, target, baseCurrency) is decimal b && b > 0)
                || spentCategoryIds.Contains(c.Id))
            .ToList();

        var monthDtos = monthKeys
            .Select(k => new BudgetHistoryMonthDto(k.Year, k.Month))
            .ToList();

        var totalOver = 0;
        var categoryDtos = new List<BudgetHistoryCategoryDto>(categories.Count);

        foreach (var c in categories)
        {
            var budget = CategoryBudgetHelper.Effective(c, target, baseCurrency) ?? 0m;
            var cells = new List<BudgetHistoryCellDto>(monthKeys.Count);
            decimal totalSpent = 0m;
            var overCount = 0;

            foreach (var (year, month) in monthKeys)
            {
                spentLookup.TryGetValue((c.Id, year, month), out var spent);
                var over = budget > 0 && spent > budget;
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
