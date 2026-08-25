using Finances.Domain.Entities;

namespace Finances.Application.Common;

/// <summary>
/// Helpers to read/write category budgets per currency. The base-currency budget is mirrored to
/// <see cref="Category.MonthlyBudget"/> for backward compatibility; all currencies live in
/// <see cref="Category.Budgets"/>.
/// </summary>
public static class CategoryBudgetHelper
{
    /// <summary>The budget for the given currency, or null when the category has none for it.</summary>
    public static decimal? Effective(Category c, string currency, string baseCurrency)
    {
        if (c.Budgets is not null && c.Budgets.TryGetValue(currency, out var v)) return v;
        if (string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase)) return c.MonthlyBudget;
        return null;
    }

    /// <summary>All budgets keyed by currency, including the legacy base-currency one.</summary>
    public static IReadOnlyDictionary<string, decimal> Merged(Category c, string baseCurrency)
    {
        var dict = new Dictionary<string, decimal>(
            c.Budgets ?? new Dictionary<string, decimal>(), StringComparer.OrdinalIgnoreCase);
        if (c.MonthlyBudget is decimal mb && mb > 0 && !dict.ContainsKey(baseCurrency))
            dict[baseCurrency] = mb;
        return dict;
    }

    /// <summary>Sets (or clears) the budget for a currency and keeps the base mirror in sync.</summary>
    public static void Set(Category c, string currency, decimal? amount, string baseCurrency)
    {
        c.Budgets ??= new Dictionary<string, decimal>();
        var clear = amount is null || amount <= 0;
        if (clear) c.Budgets.Remove(currency);
        else c.Budgets[currency] = amount!.Value;

        if (string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
            c.MonthlyBudget = clear ? null : amount;
    }
}
