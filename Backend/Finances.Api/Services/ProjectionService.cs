using Finances.Api.Data;
using Finances.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Finances.Api.Services;

/// <summary>
/// Motor de proyeccion financiera. Combina el historico mensual con un modelo
/// de regresion lineal (minimos cuadrados) para predecir el gasto del proximo
/// mes y derivar recomendaciones de ahorro y "cuanto puedo gastar".
///
/// No depende de servicios externos: es un modelo estadistico ligero pensado
/// para funcionar offline. La interfaz permite cambiarlo en el futuro por un
/// modelo de ML o un LLM sin tocar el resto de la aplicacion.
/// </summary>
public interface IProjectionService
{
    Task<ProjectionDto> BuildProjectionAsync(decimal targetSavingsRate = 0.20m, int historyMonths = 6, string lang = "en", CancellationToken ct = default);
}

public class ProjectionService : IProjectionService
{
    private readonly FinanceDbContext _db;

    public ProjectionService(FinanceDbContext db) => _db = db;

    public async Task<ProjectionDto> BuildProjectionAsync(decimal targetSavingsRate = 0.20m, int historyMonths = 6, string lang = "en", CancellationToken ct = default)
    {
        var isEs = string.Equals(lang, "es", StringComparison.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-(historyMonths - 1));

        var incomes = await _db.Incomes
            .Where(i => i.Date >= firstMonth)
            .Select(i => new { i.Date, i.Amount })
            .ToListAsync(ct);

        var expenses = await _db.Expenses
            .Where(e => e.Date >= firstMonth)
            .Select(e => new { e.Date, e.Amount })
            .ToListAsync(ct);

        // Serie mensual continua (rellena meses sin movimientos con 0).
        var history = new List<MonthPointDto>();
        for (var i = 0; i < historyMonths; i++)
        {
            var month = firstMonth.AddMonths(i);
            var inc = incomes.Where(x => x.Date.Year == month.Year && x.Date.Month == month.Month).Sum(x => x.Amount);
            var exp = expenses.Where(x => x.Date.Year == month.Year && x.Date.Month == month.Month).Sum(x => x.Amount);
            history.Add(new MonthPointDto(month.Year, month.Month, inc, exp, inc - exp, false));
        }

        var totalIncome = await _db.Incomes.SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
        var totalExpense = await _db.Expenses.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var currentBalance = totalIncome - totalExpense;

        // Promedios sobre meses que tuvieron algun movimiento, para no diluir.
        var activeMonths = history.Where(h => h.Income > 0 || h.Expense > 0).ToList();
        var avgIncome = activeMonths.Count > 0 ? activeMonths.Average(h => h.Income) : 0m;
        var avgExpense = activeMonths.Count > 0 ? activeMonths.Average(h => h.Expense) : 0m;

        // Prediccion del gasto del proximo mes mediante regresion lineal.
        var projectedExpense = ForecastNext(history.Select(h => h.Expense).ToList());
        if (projectedExpense < 0) projectedExpense = 0m;
        if (activeMonths.Count < 2) projectedExpense = avgExpense; // pocos datos: usar promedio

        var nextMonthDate = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var projectedIncome = ForecastNext(history.Select(h => h.Income).ToList());
        if (projectedIncome < 0) projectedIncome = 0m;
        if (activeMonths.Count < 2) projectedIncome = avgIncome;
        history.Add(new MonthPointDto(nextMonthDate.Year, nextMonthDate.Month, Round(projectedIncome), Round(projectedExpense), Round(projectedIncome - projectedExpense), true));

        // Datos del mes en curso.
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var spentThisMonth = expenses.Where(x => x.Date >= monthStart).Sum(x => x.Amount);
        var incomeThisMonth = incomes.Where(x => x.Date >= monthStart).Sum(x => x.Amount);
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var daysRemaining = Math.Max(1, daysInMonth - now.Day + 1);

        // Ingreso de referencia para el mes (real si existe, si no el promedio).
        var referenceIncome = incomeThisMonth > 0 ? incomeThisMonth : avgIncome;

        // Ahorro recomendado: porcentaje objetivo del ingreso de referencia.
        var recommendedSavings = Round(referenceIncome * targetSavingsRate);

        // Cuanto puedo gastar este mes sin comprometer el ahorro objetivo.
        var budgetForSpending = referenceIncome - recommendedSavings;
        var safeToSpend = Round(budgetForSpending - spentThisMonth);
        if (safeToSpend < 0) safeToSpend = 0m;
        // Nunca recomendar gastar mas de lo que realmente hay disponible.
        if (safeToSpend > currentBalance) safeToSpend = Round(Math.Max(0, currentBalance));

        var safePerDay = Round(safeToSpend / daysRemaining);

        var trend = DetermineTrend(projectedExpense, avgExpense);
        var insights = BuildInsights(isEs, currentBalance, referenceIncome, avgExpense, projectedExpense,
            recommendedSavings, safeToSpend, spentThisMonth, trend, targetSavingsRate);

        return new ProjectionDto(
            CurrentBalance: Round(currentBalance),
            AvgMonthlyIncome: Round(avgIncome),
            AvgMonthlyExpense: Round(avgExpense),
            ProjectedExpenseNextMonth: Round(projectedExpense),
            RecommendedSavings: recommendedSavings,
            SafeToSpend: safeToSpend,
            SafeToSpendPerDayRemaining: safePerDay,
            DaysRemainingInMonth: daysRemaining,
            SpentThisMonth: Round(spentThisMonth),
            Trend: trend,
            Insights: insights,
            History: history);
    }

    /// <summary>
    /// Pronostico del siguiente valor con regresion lineal por minimos cuadrados.
    /// Si la pendiente no es fiable (pocos puntos) cae al promedio simple.
    /// </summary>
    private static decimal ForecastNext(IReadOnlyList<decimal> series)
    {
        var points = series.Where(v => v != 0).Count();
        if (series.Count == 0) return 0m;
        if (points < 2) return series.DefaultIfEmpty(0).Average();

        var n = series.Count;
        double sumX = 0, sumY = 0, sumXy = 0, sumX2 = 0;
        for (var i = 0; i < n; i++)
        {
            double x = i;
            double y = (double)series[i];
            sumX += x;
            sumY += y;
            sumXy += x * y;
            sumX2 += x * x;
        }

        var denom = (n * sumX2) - (sumX * sumX);
        if (Math.Abs(denom) < 1e-9) return series.Average();

        var slope = ((n * sumXy) - (sumX * sumY)) / denom;
        var intercept = (sumY - (slope * sumX)) / n;
        var predicted = intercept + (slope * n); // x = n => siguiente mes

        // Suavizado: mezcla la regresion con el promedio para evitar saltos bruscos.
        var avg = (double)series.Average();
        var blended = (0.6 * predicted) + (0.4 * avg);
        return (decimal)Math.Max(0, blended);
    }

    /// <summary>Devuelve una clave estable de tendencia: "up", "down" o "stable".</summary>
    private static string DetermineTrend(decimal projected, decimal avg)
    {
        if (avg == 0) return "stable";
        var change = (projected - avg) / avg;
        if (change > 0.05m) return "up";
        if (change < -0.05m) return "down";
        return "stable";
    }

    private static List<string> BuildInsights(
        bool isEs, decimal balance, decimal income, decimal avgExpense, decimal projectedExpense,
        decimal recommendedSavings, decimal safeToSpend, decimal spentThisMonth,
        string trend, decimal targetSavingsRate)
    {
        var tips = new List<string>();
        var pct = (int)Math.Round(targetSavingsRate * 100);

        if (income <= 0)
        {
            tips.Add(isEs
                ? "Aun no hay ingresos registrados. Agrega tus ingresos para obtener proyecciones precisas."
                : "No income recorded yet. Add your income to get accurate projections.");
            return tips;
        }

        tips.Add(isEs
            ? $"Con tus ingresos podrias ahorrar alrededor de {recommendedSavings:N2} al mes (objetivo {pct}%)."
            : $"With your income you could save around {recommendedSavings:N2} per month (target {pct}%).");

        if (safeToSpend > 0)
            tips.Add(isEs
                ? $"Te quedan {safeToSpend:N2} para gastar este mes manteniendo tu meta de ahorro."
                : $"You have {safeToSpend:N2} left to spend this month while keeping your savings goal.");
        else
            tips.Add(isEs
                ? "Ya alcanzaste tu limite de gasto del mes segun tu meta de ahorro. Evita nuevos gastos no esenciales."
                : "You have reached your monthly spending limit based on your savings goal. Avoid new non-essential spending.");

        if (projectedExpense > income)
            tips.Add(isEs
                ? $"Alerta: el gasto proyectado ({projectedExpense:N2}) supera tu ingreso ({income:N2}). Revisa tus categorias."
                : $"Warning: projected spending ({projectedExpense:N2}) exceeds your income ({income:N2}). Review your categories.");

        tips.Add(trend switch
        {
            "up" => isEs
                ? "Tu gasto muestra tendencia al alza; considera recortar en las categorias mas variables."
                : "Your spending is trending up; consider cutting back on your most variable categories.",
            "down" => isEs
                ? "Buen trabajo: tu gasto tiende a la baja respecto a meses anteriores."
                : "Great job: your spending is trending down compared to previous months.",
            _ => isEs
                ? "Tu gasto se mantiene estable mes a mes."
                : "Your spending stays stable month to month."
        });

        if (balance < avgExpense)
            tips.Add(isEs
                ? "Tu saldo actual es menor que un mes promedio de gastos. Prioriza construir un fondo de emergencia."
                : "Your current balance is lower than an average month of expenses. Prioritize building an emergency fund.");

        return tips;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
