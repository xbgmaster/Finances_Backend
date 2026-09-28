using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class ExpenseScheduleService : IExpenseScheduleService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public ExpenseScheduleService(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
    }

    public async Task<IReadOnlyList<ExpenseScheduleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var today = DateTime.UtcNow;
        var list = await _db.ExpenseSchedules
            .Include(s => s.Category)
            .Include(s => s.PaymentMethod)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Active)
            .ThenBy(s => s.DayOfMonth)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);
        return list.Select(s => Map(s, today)).ToList();
    }

    public async Task<ExpenseScheduleDto> CreateAsync(ExpenseScheduleCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var currency = string.IsNullOrWhiteSpace(dto.Currency)
            ? baseCurrency
            : dto.Currency.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationException("El nombre de la suscripción es obligatorio.");
        if (dto.Amount <= 0)
            throw new ValidationException("El monto debe ser mayor que cero.");

        var (frequency, day, second) = ValidateSchedule(dto.PayFrequency, dto.DayOfMonth, dto.SecondDayOfMonth);
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.UserId == userId, ct)
            ?? throw new NotFoundException("La categoría indicada no existe.");
        var pm = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == dto.PaymentMethodId && p.UserId == userId, ct)
            ?? throw new NotFoundException("El medio de pago indicado no existe.");

        var today = DateTime.UtcNow;
        var schedule = new ExpenseSchedule
        {
            Name = dto.Name.Trim(),
            Amount = Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero),
            Currency = currency,
            CategoryId = category.Id,
            PaymentMethodId = pm.Id,
            PayFrequency = frequency,
            DayOfMonth = day,
            SecondDayOfMonth = second,
            AutoPost = dto.AutoPost,
            Active = true,
            UserId = userId,
            CreatedAt = today,
        };
        schedule.LastPostedPeriod = ExpenseSchedulePoster.SeedLastPostedPeriod(schedule, today);
        _db.ExpenseSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        schedule.Category = category;
        schedule.PaymentMethod = pm;
        return Map(schedule, today);
    }

    public async Task<ExpenseScheduleDto> UpdateAsync(int id, ExpenseScheduleUpdateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;

        var schedule = await _db.ExpenseSchedules
            .Include(s => s.Category)
            .Include(s => s.PaymentMethod)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("La suscripción no existe.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationException("El nombre de la suscripción es obligatorio.");
        if (dto.Amount <= 0)
            throw new ValidationException("El monto debe ser mayor que cero.");

        var (frequency, day, second) = ValidateSchedule(dto.PayFrequency, dto.DayOfMonth, dto.SecondDayOfMonth);
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.UserId == userId, ct)
            ?? throw new NotFoundException("La categoría indicada no existe.");
        var pm = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == dto.PaymentMethodId && p.UserId == userId, ct)
            ?? throw new NotFoundException("El medio de pago indicado no existe.");

        schedule.Name = dto.Name.Trim();
        schedule.Amount = Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero);
        schedule.Currency = string.IsNullOrWhiteSpace(dto.Currency)
            ? baseCurrency
            : dto.Currency.Trim().ToUpperInvariant();
        schedule.CategoryId = category.Id;
        schedule.PaymentMethodId = pm.Id;
        schedule.PayFrequency = frequency;
        schedule.DayOfMonth = day;
        schedule.SecondDayOfMonth = second;
        schedule.AutoPost = dto.AutoPost;
        schedule.Active = dto.Active;
        schedule.Category = category;
        schedule.PaymentMethod = pm;

        await _db.SaveChangesAsync(ct);
        return Map(schedule, DateTime.UtcNow);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var schedule = await _db.ExpenseSchedules.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("La suscripción no existe.");
        _db.ExpenseSchedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> PostDueForCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var today = DateTime.UtcNow;
        var schedules = await _db.ExpenseSchedules
            .Where(s => s.UserId == userId && s.Active && s.AutoPost)
            .ToListAsync(ct);
        var posted = 0;
        foreach (var s in schedules)
            posted += ExpenseSchedulePoster.PostDue(_db, s, today);
        if (posted > 0) await _db.SaveChangesAsync(ct);
        return posted;
    }

    private static (PayFrequency Frequency, int Day, int? Second) ValidateSchedule(
        string? payFrequency, int? dayOfMonth, int? secondDayOfMonth)
    {
        var frequency = ParseFrequency(payFrequency);
        var day = dayOfMonth is >= 1 and <= 31
            ? dayOfMonth.Value
            : throw new ValidationException("El día del mes debe estar entre 1 y 31.");

        int? second = null;
        if (frequency == PayFrequency.SemiMonthly)
        {
            second = secondDayOfMonth is >= 1 and <= 31
                ? secondDayOfMonth.Value
                : throw new ValidationException("El segundo día del mes es obligatorio para cobros quincenales.");
            if (second.Value <= day)
                throw new ValidationException("El segundo día debe ser mayor que el primero.");
        }

        // Weekly / Biweekly not supported for subscriptions in v1.
        if (frequency is PayFrequency.Weekly or PayFrequency.Biweekly)
            throw new ValidationException("Las suscripciones solo admiten frecuencia mensual o quincenal.");

        return (frequency, day, second);
    }

    private static PayFrequency ParseFrequency(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "semimonthly" or "semi-monthly" or "twiceamonth" => PayFrequency.SemiMonthly,
            "weekly" => PayFrequency.Weekly,
            "biweekly" => PayFrequency.Biweekly,
            _ => PayFrequency.Monthly,
        };

    private static ExpenseScheduleDto Map(ExpenseSchedule s, DateTime today) => new(
        s.Id, s.Name, s.Amount, s.Currency,
        s.CategoryId, s.Category?.Name ?? "", s.Category?.Icon, s.Category?.Color,
        s.PaymentMethodId, s.PaymentMethod?.Name ?? "", s.PaymentMethod?.Type.ToString() ?? "",
        s.PayFrequency.ToString(), s.DayOfMonth, s.SecondDayOfMonth,
        s.AutoPost, s.Active,
        ExpenseSchedulePoster.NextChargeDate(s, today),
        s.LastPostedPeriod);
}
