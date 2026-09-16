using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class ExchangeService : IExchangeService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public ExchangeService(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
    }

    public async Task<IReadOnlyList<ExchangeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.CurrencyExchanges
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Select(x => new ExchangeDto(
                x.Id, x.Date, x.FromCurrency, x.FromAmount, x.ToCurrency, x.ToAmount, x.Rate, x.Note,
                x.FromPaymentMethodId, x.FromPaymentMethod != null ? x.FromPaymentMethod.Name : null,
                x.ToPaymentMethodId, x.ToPaymentMethod != null ? x.ToPaymentMethod.Name : null))
            .ToListAsync(ct);
    }

    public async Task<ExchangeDto> CreateAsync(ExchangeCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;

        var from = dto.FromCurrency.Trim().ToUpperInvariant();
        var to = dto.ToCurrency.Trim().ToUpperInvariant();
        if (from == to)
            throw new ValidationException("Las monedas de origen y destino deben ser diferentes.");

        var rate = dto.FromAmount > 0 ? Math.Round(dto.ToAmount / dto.FromAmount, 6) : 0m;

        // Attribute the money to accounts so balances reflect it and the user can see where it
        // went. The destination always lands somewhere (a default Cash account is created if the
        // currency has none); the source is optional.
        var fromAccount = await ResolveAccountAsync(dto.FromPaymentMethodId, from, baseCurrency, userId, createIfMissing: false, ct);
        var toAccount = await ResolveAccountAsync(dto.ToPaymentMethodId, to, baseCurrency, userId, createIfMissing: true, ct);

        var exchange = new CurrencyExchange
        {
            Date = dto.Date ?? DateTime.UtcNow,
            FromCurrency = from,
            FromAmount = dto.FromAmount,
            ToCurrency = to,
            ToAmount = dto.ToAmount,
            Rate = rate,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            FromPaymentMethod = fromAccount,
            ToPaymentMethod = toAccount,
            UserId = userId
        };
        _db.CurrencyExchanges.Add(exchange);
        await _db.SaveChangesAsync(ct);

        return new ExchangeDto(
            exchange.Id, exchange.Date, exchange.FromCurrency, exchange.FromAmount,
            exchange.ToCurrency, exchange.ToAmount, exchange.Rate, exchange.Note,
            fromAccount?.Id, fromAccount?.Name, toAccount?.Id, toAccount?.Name);
    }

    /// <summary>
    /// Resolves the cash/debit account for one leg of an exchange. If an explicit id is given it
    /// is validated (owner, not a credit card, same currency). Otherwise the currency's
    /// favorite/first account is used, creating a default Cash account when
    /// <paramref name="createIfMissing"/> is set and the currency has none.
    /// </summary>
    private async Task<PaymentMethod?> ResolveAccountAsync(
        int? providedId, string currency, string baseCurrency, string userId, bool createIfMissing, CancellationToken ct)
    {
        if (providedId is not null)
        {
            var acc = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == providedId && p.UserId == userId, ct)
                ?? throw new NotFoundException("La cuenta indicada no existe.");
            if (acc.Type == PaymentMethodType.CreditCard)
                throw new ValidationException("El dinero debe entrar o salir de una cuenta de efectivo o débito.");
            if (!string.Equals(acc.Currency ?? baseCurrency, currency, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("La cuenta debe estar en la misma moneda del cambio.");
            return acc;
        }

        var candidates = await _db.PaymentMethods
            .Where(p => p.UserId == userId && !p.Archived && p.Type != PaymentMethodType.CreditCard)
            .ToListAsync(ct);
        var match = candidates
            .Where(p => string.Equals(p.Currency ?? baseCurrency, currency, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.IsFavorite)
            .ThenBy(p => p.Id)
            .FirstOrDefault();
        if (match is not null || !createIfMissing) return match;

        var cash = new PaymentMethod
        {
            Name = PaymentMethod.DefaultCashName,
            Type = PaymentMethodType.Cash,
            Currency = currency,
            Color = "#10b981",
            Icon = "cash",
            UserId = userId
        };
        _db.PaymentMethods.Add(cash);
        return cash;
    }

    public async Task<ExchangeDto> UpdateAsync(int id, ExchangeCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var exchange = await _db.CurrencyExchanges.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("El cambio de divisa no existe.");

        var from = dto.FromCurrency.Trim().ToUpperInvariant();
        var to = dto.ToCurrency.Trim().ToUpperInvariant();
        if (from == to)
            throw new ValidationException("Las monedas de origen y destino deben ser diferentes.");

        var rate = dto.FromAmount > 0 ? Math.Round(dto.ToAmount / dto.FromAmount, 6) : 0m;

        var fromAccount = await ResolveAccountAsync(dto.FromPaymentMethodId, from, baseCurrency, userId, createIfMissing: false, ct);
        var toAccount = await ResolveAccountAsync(dto.ToPaymentMethodId, to, baseCurrency, userId, createIfMissing: true, ct);

        exchange.Date = dto.Date ?? exchange.Date;
        exchange.FromCurrency = from;
        exchange.FromAmount = dto.FromAmount;
        exchange.ToCurrency = to;
        exchange.ToAmount = dto.ToAmount;
        exchange.Rate = rate;
        exchange.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        exchange.FromPaymentMethod = fromAccount;   // nav assignment lets EF fix up the FK
        exchange.ToPaymentMethod = toAccount;
        if (fromAccount is null) exchange.FromPaymentMethodId = null;
        await _db.SaveChangesAsync(ct);

        return new ExchangeDto(
            exchange.Id, exchange.Date, exchange.FromCurrency, exchange.FromAmount,
            exchange.ToCurrency, exchange.ToAmount, exchange.Rate, exchange.Note,
            fromAccount?.Id, fromAccount?.Name, toAccount?.Id, toAccount?.Name);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var exchange = await _db.CurrencyExchanges.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("El cambio de divisa no existe.");
        _db.CurrencyExchanges.Remove(exchange);
        await _db.SaveChangesAsync(ct);
    }
}
