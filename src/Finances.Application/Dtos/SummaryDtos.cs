namespace Finances.Application.Dtos;

public record BalanceDto(decimal TotalIncome, decimal TotalExpense, decimal Balance);

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
