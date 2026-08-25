using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public CategoryService(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
    }

    private CategoryDto Map(Category c, string baseCurrency) =>
        new(c.Id, c.Name, c.Icon, c.Color, c.MonthlyBudget, c.IsSystem,
            CategoryBudgetHelper.Merged(c, baseCurrency));

    private string NormalizeCurrency(string? requested, string baseCurrency) =>
        string.IsNullOrWhiteSpace(requested) ? baseCurrency : requested.Trim().ToUpperInvariant();

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var categories = await _db.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return categories.Select(c => Map(c, baseCurrency)).ToList();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var c = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        return c is null ? null : Map(c, baseCurrency);
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var category = new Category
        {
            Name = dto.Name.Trim(),
            Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "tag" : dto.Icon.Trim(),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? "#6366f1" : dto.Color.Trim(),
            UserId = userId
        };
        CategoryBudgetHelper.Set(category, NormalizeCurrency(dto.BudgetCurrency, baseCurrency), dto.MonthlyBudget, baseCurrency);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return Map(category, baseCurrency);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var category = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("La categoria no existe.");

        if (category.IsSystem)
            throw new ConflictException("Esta es una categoria del sistema y no se puede modificar.");

        category.Name = dto.Name.Trim();
        category.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? category.Icon : dto.Icon.Trim();
        category.Color = string.IsNullOrWhiteSpace(dto.Color) ? category.Color : dto.Color.Trim();
        CategoryBudgetHelper.Set(category, NormalizeCurrency(dto.BudgetCurrency, baseCurrency), dto.MonthlyBudget, baseCurrency);
        await _db.SaveChangesAsync(ct);
        return Map(category, baseCurrency);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var category = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("La categoria no existe.");

        if (category.IsSystem)
            throw new ConflictException("Esta es una categoria del sistema y no se puede eliminar.");

        var hasExpenses = await _db.Expenses.AnyAsync(e => e.CategoryId == id, ct);
        if (hasExpenses)
            throw new ConflictException("No se puede eliminar una categoria con gastos asociados.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
    }
}
