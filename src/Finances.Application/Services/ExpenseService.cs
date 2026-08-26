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
    private readonly IProfileService _profile;

    public ExpenseService(IFinanceDbContext db, IFileStorage storage, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _storage = storage;
        _current = current;
        _profile = profile;
    }

    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(int? year, int? month, string? currency, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var query = _db.Expenses.Include(e => e.Category).Where(e => e.UserId == userId);
        if (year is not null) query = query.Where(e => e.Date.Year == year);
        if (month is not null) query = query.Where(e => e.Date.Month == month);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var cur = currency.Trim().ToUpperInvariant();
            // A null stored currency means "base currency".
            query = cur == baseCurrency.ToUpperInvariant()
                ? query.Where(e => e.Currency == null || e.Currency == cur)
                : query.Where(e => e.Currency == cur);
        }

        return await query
            .OrderByDescending(e => e.Date)
            .Select(e => new ExpenseDto(
                e.Id, e.Amount, e.Description, e.Date,
                e.CategoryId, e.Category!.Name, e.Category.Icon, e.Category.Color,
                e.ReceiptUrl, e.Currency ?? baseCurrency))
            .ToListAsync(ct);
    }

    public async Task<ExpenseDto> CreateAsync(ExpenseCreateDto dto, FileUpload? receipt, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var currency = string.IsNullOrWhiteSpace(dto.Currency)
            ? baseCurrency
            : dto.Currency.Trim().ToUpperInvariant();
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
            Currency = currency,
            UserId = userId
        };
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);

        return new ExpenseDto(
            expense.Id, expense.Amount, expense.Description, expense.Date,
            category.Id, category.Name, category.Icon, category.Color,
            expense.ReceiptUrl, expense.Currency ?? baseCurrency);
    }

    public async Task<ExpenseDto> UpdateAsync(int id, ExpenseUpdateDto dto, FileUpload? receipt, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;

        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct)
            ?? throw new NotFoundException("El gasto no existe.");

        // Credit-payment mirrors are managed from the Credits section to keep them in sync.
        if (expense.CreditPaymentId is not null)
            throw new ConflictException("Este gasto proviene de un pago de credito. Modificalo desde la seccion de Creditos.");

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.UserId == userId, ct)
            ?? throw new NotFoundException("La categoria indicada no existe.");

        // Receipt: a new upload replaces the old file; otherwise honor an explicit removal.
        if (receipt is { Length: > 0 })
        {
            if (receipt.Length > MaxReceiptBytes)
                throw new ValidationException("La imagen supera el tamano maximo de 5 MB.");
            if (!AllowedImageTypes.Contains(receipt.ContentType))
                throw new ValidationException("Formato de imagen no permitido (usa JPG, PNG, WEBP o GIF).");

            if (!string.IsNullOrEmpty(expense.ReceiptUrl))
                _storage.Delete(expense.ReceiptUrl);
            expense.ReceiptUrl = await _storage.SaveAsync(receipt, "uploads", ct);
        }
        else if (dto.RemoveReceipt && !string.IsNullOrEmpty(expense.ReceiptUrl))
        {
            _storage.Delete(expense.ReceiptUrl);
            expense.ReceiptUrl = null;
        }

        expense.Amount = dto.Amount;
        expense.Description = dto.Description?.Trim() ?? string.Empty;
        expense.Date = dto.Date ?? expense.Date;
        expense.CategoryId = dto.CategoryId;
        expense.Currency = string.IsNullOrWhiteSpace(dto.Currency)
            ? baseCurrency
            : dto.Currency.Trim().ToUpperInvariant();

        await _db.SaveChangesAsync(ct);

        return new ExpenseDto(
            expense.Id, expense.Amount, expense.Description, expense.Date,
            category.Id, category.Name, category.Icon, category.Color,
            expense.ReceiptUrl, expense.Currency ?? baseCurrency);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct)
            ?? throw new NotFoundException("El gasto no existe.");

        if (expense.CreditPaymentId is not null)
            throw new ConflictException("Este gasto proviene de un pago de credito. Modificalo o eliminalo desde la seccion de Creditos.");

        if (!string.IsNullOrEmpty(expense.ReceiptUrl))
            _storage.Delete(expense.ReceiptUrl);

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync(ct);
    }
}
