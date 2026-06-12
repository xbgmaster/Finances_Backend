using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class ExpenseService : IExpenseService
{
    private static readonly string[] AllowedImageTypes =
        { "image/jpeg", "image/png", "image/webp", "image/gif" };
    private const long MaxReceiptBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IFinanceDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _current;

    public ExpenseService(IFinanceDbContext db, IFileStorage storage, ICurrentUser current)
    {
        _db = db;
        _storage = storage;
        _current = current;
    }

    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(int? year, int? month, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var query = _db.Expenses.Include(e => e.Category).Where(e => e.UserId == userId);
        if (year is not null) query = query.Where(e => e.Date.Year == year);
        if (month is not null) query = query.Where(e => e.Date.Month == month);

        return await query
            .OrderByDescending(e => e.Date)
            .Select(e => new ExpenseDto(
                e.Id, e.Amount, e.Description, e.Date,
                e.CategoryId, e.Category!.Name, e.Category.Icon, e.Category.Color,
                e.ReceiptUrl))
            .ToListAsync(ct);
    }

    public async Task<ExpenseDto> CreateAsync(ExpenseCreateDto dto, FileUpload? receipt, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.UserId == userId, ct)
            ?? throw new NotFoundException("La categoria indicada no existe.");

        string? receiptUrl = null;
        if (receipt is { Length: > 0 })
        {
            if (receipt.Length > MaxReceiptBytes)
                throw new ValidationException("La imagen supera el tamano maximo de 5 MB.");
            if (!AllowedImageTypes.Contains(receipt.ContentType))
                throw new ValidationException("Formato de imagen no permitido (usa JPG, PNG, WEBP o GIF).");

            receiptUrl = await _storage.SaveAsync(receipt, "uploads", ct);
        }

        var expense = new Expense
        {
            Amount = dto.Amount,
            Description = dto.Description?.Trim() ?? string.Empty,
            Date = dto.Date ?? DateTime.UtcNow,
            CategoryId = dto.CategoryId,
            ReceiptUrl = receiptUrl,
            UserId = userId
        };
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);

        return new ExpenseDto(
            expense.Id, expense.Amount, expense.Description, expense.Date,
            category.Id, category.Name, category.Icon, category.Color,
            expense.ReceiptUrl);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct)
            ?? throw new NotFoundException("El gasto no existe.");

        if (!string.IsNullOrEmpty(expense.ReceiptUrl))
            _storage.Delete(expense.ReceiptUrl);

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync(ct);
    }
}
