using Finances.Api.Data;
using Finances.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BalanceController : ControllerBase
{
    private readonly FinanceDbContext _db;

    public BalanceController(FinanceDbContext db) => _db = db;

    /// <summary>Saldo global: ingresos totales menos gastos totales.</summary>
    [HttpGet]
    public async Task<ActionResult<BalanceDto>> GetBalance()
    {
        var totalIncome = await _db.Incomes.SumAsync(i => (decimal?)i.Amount) ?? 0m;
        var totalExpense = await _db.Expenses.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        return Ok(new BalanceDto(totalIncome, totalExpense, totalIncome - totalExpense));
    }

    /// <summary>Resumen de un mes: ingreso, gasto, neto y desglose por categoria.</summary>
    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlySummaryDto>> GetMonthly([FromQuery] int? year, [FromQuery] int? month)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        var income = await _db.Incomes
            .Where(i => i.Date.Year == y && i.Date.Month == m)
            .SumAsync(i => (decimal?)i.Amount) ?? 0m;

        var monthExpenses = await _db.Expenses
            .Include(e => e.Category)
            .Where(e => e.Date.Year == y && e.Date.Month == m)
            .ToListAsync();

        var expense = monthExpenses.Sum(e => e.Amount);

        var byCategory = monthExpenses
            .GroupBy(e => e.Category!)
            .Select(g => new CategorySpendDto(
                g.Key.Id, g.Key.Name, g.Key.Icon, g.Key.Color,
                g.Sum(e => e.Amount), g.Key.MonthlyBudget))
            .OrderByDescending(c => c.Spent)
            .ToList();

        return Ok(new MonthlySummaryDto(y, m, income, expense, income - expense, byCategory));
    }
}
