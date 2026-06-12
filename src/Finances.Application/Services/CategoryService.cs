using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public CategoryService(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Icon, c.Color, c.MonthlyBudget))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var c = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        return c is null ? null : new CategoryDto(c.Id, c.Name, c.Icon, c.Color, c.MonthlyBudget);
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var category = new Category
        {
            Name = dto.Name.Trim(),
            Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "tag" : dto.Icon.Trim(),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? "#6366f1" : dto.Color.Trim(),
            MonthlyBudget = dto.MonthlyBudget,
            UserId = userId
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Icon, category.Color, category.MonthlyBudget);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var category = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("La categoria no existe.");

        category.Name = dto.Name.Trim();
        category.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? category.Icon : dto.Icon.Trim();
        category.Color = string.IsNullOrWhiteSpace(dto.Color) ? category.Color : dto.Color.Trim();
        category.MonthlyBudget = dto.MonthlyBudget;
        await _db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Icon, category.Color, category.MonthlyBudget);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var category = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("La categoria no existe.");

        var hasExpenses = await _db.Expenses.AnyAsync(e => e.CategoryId == id, ct);
        if (hasExpenses)
            throw new ConflictException("No se puede eliminar una categoria con gastos asociados.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
    }
}
