using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class IncomeService : IIncomeService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;

    public IncomeService(IFinanceDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<IncomeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        return await _db.Incomes
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.Date)
            .Select(i => new IncomeDto(i.Id, i.Amount, i.Description, i.Date))
            .ToListAsync(ct);
    }

    public async Task<IncomeDto> CreateAsync(IncomeCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var income = new Income
        {
            Amount = dto.Amount,
            Description = dto.Description?.Trim() ?? string.Empty,
            Date = dto.Date ?? DateTime.UtcNow,
            UserId = userId
        };
        _db.Incomes.Add(income);
        await _db.SaveChangesAsync(ct);
        return new IncomeDto(income.Id, income.Amount, income.Description, income.Date);
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
