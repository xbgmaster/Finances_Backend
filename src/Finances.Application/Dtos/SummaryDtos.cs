namespace Finances.Application.Dtos;

/// <summary>Income/expense/balance totals for a single currency.</summary>
public record CurrencyBalanceDto(string Currency, decimal TotalIncome, decimal TotalExpense, decimal Balance);

/// <summary>
/// Overall balance. The top-level totals are the user's base currency (kept for backward
/// compatibility with the main "available balance" card); <see cref="ByCurrency"/> breaks the
/// balance down per currency (base first) so the user can hold money in several currencies.
/// </summary>
public record BalanceDto(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Balance,
    string BaseCurrency,
    IReadOnlyList<CurrencyBalanceDto> ByCurrency);

public record CategorySpendDto(
    int CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    decimal Spent,
    decimal? MonthlyBudget);

public record MonthlySummaryDto(
    int Year,
    int Month,
    decimal Income,
    decimal Expense,
    decimal Net,
    IReadOnlyList<CategorySpendDto> ByCategory);

public record BudgetHistoryMonthDto(int Year, int Month);

public record BudgetHistoryCellDto(int Year, int Month, decimal Spent, decimal Percent, bool Over);

public record BudgetHistoryCategoryDto(
    int CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    decimal MonthlyBudget,
    decimal TotalSpent,
    int OverCount,
    IReadOnlyList<BudgetHistoryCellDto> Cells);

public record BudgetHistoryDto(
    IReadOnlyList<BudgetHistoryMonthDto> Months,
    IReadOnlyList<BudgetHistoryCategoryDto> Categories,
    int TotalOverCount);
