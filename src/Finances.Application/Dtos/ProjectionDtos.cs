namespace Finances.Application.Dtos;

public record MonthPointDto(int Year, int Month, decimal Income, decimal Expense, decimal Net, bool IsForecast);

public record ProjectionDto(
    decimal CurrentBalance,
    decimal AvgMonthlyIncome,
    decimal AvgMonthlyExpense,
    decimal ProjectedExpenseNextMonth,
    decimal RecommendedSavings,
    decimal SafeToSpend,
    decimal SafeToSpendPerDayRemaining,
    int DaysRemainingInMonth,
    decimal SpentThisMonth,
    string Trend,
    IReadOnlyList<string> Insights,
    IReadOnlyList<MonthPointDto> History);
