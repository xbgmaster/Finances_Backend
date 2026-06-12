using Finances.Api.Data;
using Finances.Api.Dtos;
using Finances.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncomesController : ControllerBase
{
    private readonly FinanceDbContext _db;

    public IncomesController(FinanceDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<IncomeDto>>> GetAll()
    {
        var items = await _db.Incomes
            .OrderByDescending(i => i.Date)
            .Select(i => new IncomeDto(i.Id, i.Amount, i.Description, i.Date))
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<IncomeDto>> Create(IncomeCreateDto dto)
    {
        var income = new Income
        {
            Amount = dto.Amount,
            Description = dto.Description?.Trim() ?? string.Empty,
            Date = dto.Date ?? DateTime.UtcNow
        };
        _db.Incomes.Add(income);
        await _db.SaveChangesAsync();
        return Ok(new IncomeDto(income.Id, income.Amount, income.Description, income.Date));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var income = await _db.Incomes.FindAsync(id);
        if (income is null) return NotFound();
        _db.Incomes.Remove(income);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
