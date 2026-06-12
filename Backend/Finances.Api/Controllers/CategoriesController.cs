using Finances.Api.Data;
using Finances.Api.Dtos;
using Finances.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly FinanceDbContext _db;

    public CategoriesController(FinanceDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll()
    {
        var items = await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Icon, c.Color, c.MonthlyBudget))
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetById(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c is null) return NotFound();
        return Ok(new CategoryDto(c.Id, c.Name, c.Icon, c.Color, c.MonthlyBudget));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryCreateDto dto)
    {
        var category = new Category
        {
            Name = dto.Name.Trim(),
            Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "tag" : dto.Icon.Trim(),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? "#6366f1" : dto.Color.Trim(),
            MonthlyBudget = dto.MonthlyBudget
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        var result = new CategoryDto(category.Id, category.Name, category.Icon, category.Color, category.MonthlyBudget);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryCreateDto dto)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category is null) return NotFound();

        category.Name = dto.Name.Trim();
        category.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? category.Icon : dto.Icon.Trim();
        category.Color = string.IsNullOrWhiteSpace(dto.Color) ? category.Color : dto.Color.Trim();
        category.MonthlyBudget = dto.MonthlyBudget;
        await _db.SaveChangesAsync();
        return Ok(new CategoryDto(category.Id, category.Name, category.Icon, category.Color, category.MonthlyBudget));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category is null) return NotFound();

        var hasExpenses = await _db.Expenses.AnyAsync(e => e.CategoryId == id);
        if (hasExpenses)
            return Conflict(new { message = "No se puede eliminar una categoria con gastos asociados." });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
