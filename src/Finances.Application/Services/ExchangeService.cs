using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class ExchangeService : IExchangeService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public ExchangeService(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<ExchangeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.CurrencyExchanges
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Select(x => new ExchangeDto(
                x.Id, x.Date, x.FromCurrency, x.FromAmount, x.ToCurrency, x.ToAmount, x.Rate, x.Note))
            .ToListAsync(ct);
    }

    public async Task<ExchangeDto> CreateAsync(ExchangeCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();

        var from = dto.FromCurrency.Trim().ToUpperInvariant();
        var to = dto.ToCurrency.Trim().ToUpperInvariant();
        if (from == to)
            throw new ValidationException("Las monedas de origen y destino deben ser diferentes.");

        var rate = dto.FromAmount > 0 ? Math.Round(dto.ToAmount / dto.FromAmount, 6) : 0m;

        var exchange = new CurrencyExchange
        {
            Date = dto.Date ?? DateTime.UtcNow,
            FromCurrency = from,
            FromAmount = dto.FromAmount,
            ToCurrency = to,
            ToAmount = dto.ToAmount,
            Rate = rate,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            UserId = userId
        };
        _db.CurrencyExchanges.Add(exchange);
        await _db.SaveChangesAsync(ct);

        return new ExchangeDto(
            exchange.Id, exchange.Date, exchange.FromCurrency, exchange.FromAmount,
            exchange.ToCurrency, exchange.ToAmount, exchange.Rate, exchange.Note);
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
