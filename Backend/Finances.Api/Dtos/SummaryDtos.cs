namespace Finances.Api.Dtos;

/// <summary>Resumen global: saldo total disponible.</summary>
public record BalanceDto(decimal TotalIncome, decimal TotalExpense, decimal Balance);

/// <summary>Gasto agrupado por categoria en un periodo.</summary>
public record CategorySpendDto(
    int CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    decimal Spent,
    decimal? MonthlyBudget);

/// <summary>Balance de un mes concreto.</summary>
public record MonthlySummaryDto(
    int Year,
    int Month,
    decimal Income,
    decimal Expense,
    decimal Net,
    IReadOnlyList<CategorySpendDto> ByCategory);
