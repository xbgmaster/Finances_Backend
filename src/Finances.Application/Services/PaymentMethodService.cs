using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class PaymentMethodService : IPaymentMethodService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public PaymentMethodService(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var now = DateTime.UtcNow;

        await EnsureDefaultCashAsync(userId, ct);

        var methods = await _db.PaymentMethods
            .Where(p => p.UserId == userId)
            .Where(p => includeArchived || !p.Archived)
            .OrderBy(p => p.Archived).ThenByDescending(p => p.IsFavorite).ThenBy(p => p.Name)
            .ToListAsync(ct);

        if (methods.Count == 0) return new List<PaymentMethodDto>();

        var ids = methods.Select(m => m.Id).ToList();
        var expenseAgg = await _db.Expenses
            .Where(e => e.UserId == userId && e.PaymentMethodId != null && ids.Contains(e.PaymentMethodId.Value))
            .GroupBy(e => e.PaymentMethodId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Total = g.Sum(x => x.Amount),
                Month = g.Where(x => x.Date.Year == now.Year && x.Date.Month == now.Month).Sum(x => x.Amount)
            })
            .ToListAsync(ct);

        var incomeAgg = await _db.Incomes
            .Where(i => i.UserId == userId && i.PaymentMethodId != null && ids.Contains(i.PaymentMethodId.Value))
            .GroupBy(i => i.PaymentMethodId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Total = g.Sum(x => x.Amount),
                Month = g.Where(x => x.Date.Year == now.Year && x.Date.Month == now.Month).Sum(x => x.Amount)
            })
            .ToListAsync(ct);

        var expenseById = expenseAgg.ToDictionary(a => a.Id);
        var incomeById = incomeAgg.ToDictionary(a => a.Id);
        return methods.Select(m =>
        {
            expenseById.TryGetValue(m.Id, out var exp);
            incomeById.TryGetValue(m.Id, out var inc);
            return Map(m, baseCurrency, exp?.Month ?? 0m, exp?.Total ?? 0m, inc?.Month ?? 0m, inc?.Total ?? 0m);
        }).ToList();
    }

    public async Task<PaymentMethodDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var now = DateTime.UtcNow;

        var method = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);
        if (method is null) return null;

        var (expMonth, expTotal, incMonth, incTotal) = await AggregateAsync(id, userId, now, ct);
        return Map(method, baseCurrency, expMonth, expTotal, incMonth, incTotal);
    }

    public async Task<PaymentMethodDto> CreateAsync(PaymentMethodCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var type = ParseType(dto.Type);

        var method = new PaymentMethod
        {
            Name = dto.Name.Trim(),
            Type = type,
            Currency = NormalizeCurrency(dto.Currency, baseCurrency),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? "#6366f1" : dto.Color.Trim(),
            Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "card" : dto.Icon.Trim(),
            Archived = dto.Archived,
            IsFavorite = dto.IsFavorite,
            UserId = userId
        };
        ApplyCreditCardFields(method, type, dto);

        _db.PaymentMethods.Add(method);
        if (dto.IsFavorite) await ClearOtherFavoritesAsync(userId, method, ct);
        await _db.SaveChangesAsync(ct);
        return Map(method, baseCurrency, 0m, 0m, 0m, 0m);
    }

    public async Task<PaymentMethodDto> UpdateAsync(int id, PaymentMethodCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var type = ParseType(dto.Type);

        var method = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct)
            ?? throw new NotFoundException("El medio de pago no existe.");

        method.Name = dto.Name.Trim();
        method.Type = type;
        method.Currency = NormalizeCurrency(dto.Currency, baseCurrency);
        method.Color = string.IsNullOrWhiteSpace(dto.Color) ? method.Color : dto.Color.Trim();
        method.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? method.Icon : dto.Icon.Trim();
        method.Archived = dto.Archived;
        method.IsFavorite = dto.IsFavorite;
        ApplyCreditCardFields(method, type, dto);

        if (dto.IsFavorite) await ClearOtherFavoritesAsync(userId, method, ct);
        await _db.SaveChangesAsync(ct);

        var (expMonth, expTotal, incMonth, incTotal) = await AggregateAsync(id, userId, DateTime.UtcNow, ct);
        return Map(method, baseCurrency, expMonth, expTotal, incMonth, incTotal);
    }

    /// <summary>Ensures a single favorite per user by unsetting the flag on every other method.</summary>
    private async Task ClearOtherFavoritesAsync(string userId, PaymentMethod favorite, CancellationToken ct)
    {
        var others = await _db.PaymentMethods
            .Where(p => p.UserId == userId && p.IsFavorite && p.Id != favorite.Id)
            .ToListAsync(ct);
        foreach (var o in others) o.IsFavorite = false;
    }

    /// <summary>Creates the built-in cash account the first time an existing user has none.</summary>
    private async Task EnsureDefaultCashAsync(string userId, CancellationToken ct)
    {
        var hasAny = await _db.PaymentMethods.AnyAsync(p => p.UserId == userId, ct);
        if (hasAny) return;
        _db.PaymentMethods.Add(PaymentMethod.DefaultCash(userId));
        await _db.SaveChangesAsync(ct);
    }

    private async Task<(decimal expMonth, decimal expTotal, decimal incMonth, decimal incTotal)> AggregateAsync(
        int id, string userId, DateTime now, CancellationToken ct)
    {
        var expMonth = await _db.Expenses
            .Where(e => e.UserId == userId && e.PaymentMethodId == id
                && e.Date.Year == now.Year && e.Date.Month == now.Month)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var expTotal = await _db.Expenses
            .Where(e => e.UserId == userId && e.PaymentMethodId == id)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var incMonth = await _db.Incomes
            .Where(i => i.UserId == userId && i.PaymentMethodId == id
                && i.Date.Year == now.Year && i.Date.Month == now.Month)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
        var incTotal = await _db.Incomes
            .Where(i => i.UserId == userId && i.PaymentMethodId == id)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
        return (expMonth, expTotal, incMonth, incTotal);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var method = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct)
            ?? throw new NotFoundException("El medio de pago no existe.");

        // Linked expenses/incomes keep their history: the FK is set to null on delete.
        _db.PaymentMethods.Remove(method);
        await _db.SaveChangesAsync(ct);
    }

    private static PaymentMethodDto Map(
        PaymentMethod m, string baseCurrency,
        decimal spentThisMonth, decimal totalCharged, decimal receivedThisMonth, decimal totalReceived)
    {
        var isCard = m.Type == PaymentMethodType.CreditCard;
        // Credit card: balance is the debt owed (charges minus payments; payments arrive in a
        // later phase). Debit/cash: balance is the money in the account (income minus expenses).
        var balance = isCard ? totalCharged : totalReceived - totalCharged;
        decimal? available = isCard && m.CreditLimit is not null ? m.CreditLimit - balance : null;
        return new PaymentMethodDto(
            m.Id, m.Name, m.Type.ToString(), m.Currency ?? baseCurrency, m.Color, m.Icon,
            m.CreditLimit, m.StatementDay, m.PaymentDueDay, m.Archived, m.IsFavorite,
            spentThisMonth, receivedThisMonth, balance, available);
    }

    private static void ApplyCreditCardFields(PaymentMethod method, PaymentMethodType type, PaymentMethodCreateDto dto)
    {
        if (type == PaymentMethodType.CreditCard)
        {
            method.CreditLimit = dto.CreditLimit;
            method.StatementDay = dto.StatementDay;
            method.PaymentDueDay = dto.PaymentDueDay;
        }
        else
        {
            // Debit/cash never carry a limit or statement dates.
            method.CreditLimit = null;
            method.StatementDay = null;
            method.PaymentDueDay = null;
        }
    }

    private static PaymentMethodType ParseType(string? type) =>
        Enum.TryParse<PaymentMethodType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : PaymentMethodType.Debit;

    private static string NormalizeCurrency(string? requested, string baseCurrency) =>
        string.IsNullOrWhiteSpace(requested) ? baseCurrency : requested.Trim().ToUpperInvariant();
}
