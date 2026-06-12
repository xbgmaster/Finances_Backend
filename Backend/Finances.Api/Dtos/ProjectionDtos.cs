namespace Finances.Api.Dtos;

/// <summary>Punto de la serie historica mensual (real) o proyectada.</summary>
public record MonthPointDto(int Year, int Month, decimal Income, decimal Expense, decimal Net, bool IsForecast);

/// <summary>
/// Resultado del motor de proyeccion: prediccion de gasto/ingreso, ahorro
/// recomendado y cuanto se puede gastar de forma segura.
/// </summary>
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
