using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class IncomeService : IIncomeService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public IncomeService(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
    }

    public async Task<IReadOnlyList<IncomeDto>> GetAllAsync(string? currency = null, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var query = _db.Incomes.Where(i => i.UserId == userId);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var cur = currency.Trim().ToUpperInvariant();
            query = cur == baseCurrency.ToUpperInvariant()
                ? query.Where(i => i.Currency == null || i.Currency == cur)
                : query.Where(i => i.Currency == cur);
        }
        return await query
            .OrderByDescending(i => i.Date)
            .Select(i => new IncomeDto(i.Id, i.Amount, i.Description, i.Date, i.Currency ?? baseCurrency))
            .ToListAsync(ct);
    }

    public async Task<IncomeDto> CreateAsync(IncomeCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var currency = string.IsNullOrWhiteSpace(dto.Currency)
            ? baseCurrency
            : dto.Currency.Trim().ToUpperInvariant();

        var income = new Income
        {
            Amount = dto.Amount,
            Description = dto.Description?.Trim() ?? string.Empty,
            Date = dto.Date ?? DateTime.UtcNow,
            Currency = currency,
            UserId = userId
        };
        _db.Incomes.Add(income);
        await _db.SaveChangesAsync(ct);
        return new IncomeDto(income.Id, income.Amount, income.Description, income.Date, income.Currency ?? baseCurrency);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var income = await _db.Incomes.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, ct)
            ?? throw new NotFoundException("El ingreso no existe.");
        _db.Incomes.Remove(income);
        await _db.SaveChangesAsync(ct);
    }
}
