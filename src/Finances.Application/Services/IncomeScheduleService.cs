using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Services;

public class IncomeScheduleService : IIncomeScheduleService
{
    private readonly IFinanceDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IProfileService _profile;

    public IncomeScheduleService(IFinanceDbContext db, ICurrentUser current, IProfileService profile)
    {
        _db = db;
        _current = current;
        _profile = profile;
    }

    public async Task<IReadOnlyList<IncomeScheduleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var today = DateTime.UtcNow;
        var schedules = await _db.IncomeSchedules
            .Include(s => s.PaymentMethod)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Active)
            .ThenBy(s => s.DayOfMonth)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        return schedules.Select(s => Map(s, today)).ToList();
    }

    public async Task<IncomeScheduleDto> CreateAsync(IncomeScheduleCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;
        var currency = string.IsNullOrWhiteSpace(dto.Currency)
            ? baseCurrency
            : dto.Currency.Trim().ToUpperInvariant();

        var payType = ParsePayType(dto.PayType);
        var (amount, hourlyRate) = ValidatePay(payType, dto.Amount, dto.HourlyRate);
        var (frequency, dayOfMonth, secondDay, anchorDate) =
            ValidateSchedule(dto.PayFrequency, dto.DayOfMonth, dto.SecondDayOfMonth, dto.AnchorDate);

        var paymentMethod = await ResolvePaymentMethodAsync(dto.PaymentMethodId, userId, ct);
        var today = DateTime.UtcNow;

        var schedule = new IncomeSchedule
        {
            Name = dto.Name.Trim(),
            PayType = payType,
            PayFrequency = frequency,
            Amount = amount,
            HourlyRate = hourlyRate,
            Currency = currency,
            Color = NormalizeColor(dto.Color),
            PaymentMethodId = paymentMethod?.Id,
            DayOfMonth = dayOfMonth,
            SecondDayOfMonth = secondDay,
            AnchorDate = anchorDate,
            AutoPost = dto.AutoPost,
            Active = true,
            UserId = userId,
            CreatedAt = today,
        };
        schedule.LastPostedPeriod = IncomeSchedulePoster.SeedLastPostedPeriod(schedule, today);
        _db.IncomeSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        schedule.PaymentMethod = paymentMethod;
        return Map(schedule, today);
    }

    public async Task<IncomeScheduleDto> UpdateAsync(int id, IncomeScheduleUpdateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var baseCurrency = (await _profile.GetAsync(ct)).Currency;

        var schedule = await _db.IncomeSchedules
            .Include(s => s.PaymentMethod)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("El trabajo indicado no existe.");

        var payType = ParsePayType(dto.PayType);
        var (amount, hourlyRate) = ValidatePay(payType, dto.Amount, dto.HourlyRate);
        var (frequency, dayOfMonth, secondDay, anchorDate) =
            ValidateSchedule(dto.PayFrequency, dto.DayOfMonth, dto.SecondDayOfMonth, dto.AnchorDate);

        var paymentMethod = await ResolvePaymentMethodAsync(dto.PaymentMethodId, userId, ct);

        schedule.Name = dto.Name.Trim();
        schedule.PayType = payType;
        schedule.PayFrequency = frequency;
        schedule.Amount = amount;
        schedule.HourlyRate = hourlyRate;
        schedule.Currency = string.IsNullOrWhiteSpace(dto.Currency)
            ? baseCurrency
            : dto.Currency.Trim().ToUpperInvariant();
        schedule.Color = NormalizeColor(dto.Color);
        schedule.PaymentMethodId = paymentMethod?.Id;
        schedule.DayOfMonth = dayOfMonth;
        schedule.SecondDayOfMonth = secondDay;
        schedule.AnchorDate = anchorDate;
        schedule.AutoPost = dto.AutoPost;
        schedule.Active = dto.Active;

        await _db.SaveChangesAsync(ct);

        schedule.PaymentMethod = paymentMethod;
        return Map(schedule, DateTime.UtcNow);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var schedule = await _db.IncomeSchedules.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new NotFoundException("El trabajo indicado no existe.");
        _db.IncomeSchedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> PostDueForCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var today = DateTime.UtcNow;
        var schedules = await _db.IncomeSchedules
            .Where(s => s.UserId == userId && s.Active && s.AutoPost)
            .ToListAsync(ct);
        if (schedules.Count == 0) return 0;

        // Preload unposted shifts for the hourly jobs so the poster can total each pay cut.
        var hourlyIds = schedules.Where(s => s.PayType == PayType.Hourly).Select(s => s.Id).ToList();
        var shiftsByJob = await LoadUnpostedShiftsAsync(hourlyIds, ct);

        var posted = schedules.Sum(s => IncomeSchedulePoster.PostDue(
            _db, s, today, shiftsByJob.TryGetValue(s.Id, out var list) ? list : null));
        if (posted > 0) await _db.SaveChangesAsync(ct);
        return posted;
    }

    // ---- Work shifts (hourly jobs) ------------------------------------------------------------

    public async Task<IReadOnlyList<WorkShiftDto>> GetShiftsAsync(int year, int month, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var shifts = await _db.WorkShifts
            .Include(w => w.IncomeSchedule)
            .Where(w => w.UserId == userId && w.Date >= start && w.Date < end)
            .OrderBy(w => w.Date)
            .ToListAsync(ct);
        return shifts.Select(MapShift).ToList();
    }

    public async Task<WorkShiftDto> CreateShiftAsync(WorkShiftCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var job = await _db.IncomeSchedules.FirstOrDefaultAsync(s => s.Id == dto.IncomeScheduleId && s.UserId == userId, ct)
            ?? throw new NotFoundException("El trabajo indicado no existe.");
        if (job.PayType != PayType.Hourly)
            throw new ValidationException("Solo los trabajos por horas admiten turnos.");

        var rate = dto.HourlyRate ?? job.HourlyRate
            ?? throw new ValidationException("Indica un valor por hora.");
        if (rate <= 0) throw new ValidationException("El valor por hora debe ser mayor que cero.");

        var shift = new WorkShift
        {
            IncomeScheduleId = job.Id,
            Date = dto.Date.Date,
            Hours = dto.Hours,
            HourlyRate = rate,
            Amount = Math.Round(dto.Hours * rate, 2, MidpointRounding.AwayFromZero),
            Currency = job.Currency,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        _db.WorkShifts.Add(shift);
        await _db.SaveChangesAsync(ct);

        shift.IncomeSchedule = job;
        return MapShift(shift);
    }

    public async Task<WorkShiftDto> UpdateShiftAsync(int id, WorkShiftUpdateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var shift = await _db.WorkShifts
            .Include(w => w.IncomeSchedule)
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw new NotFoundException("El turno indicado no existe.");
        if (shift.IncomeId != null)
            throw new ValidationException("No puedes editar un turno que ya fue pagado.");

        var rate = dto.HourlyRate ?? shift.HourlyRate;
        if (rate <= 0) throw new ValidationException("El valor por hora debe ser mayor que cero.");

        shift.Date = dto.Date.Date;
        shift.Hours = dto.Hours;
        shift.HourlyRate = rate;
        shift.Amount = Math.Round(dto.Hours * rate, 2, MidpointRounding.AwayFromZero);
        await _db.SaveChangesAsync(ct);
        return MapShift(shift);
    }

    public async Task DeleteShiftAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var shift = await _db.WorkShifts.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct)
            ?? throw new NotFoundException("El turno indicado no existe.");
        if (shift.IncomeId != null)
            throw new ValidationException("No puedes eliminar un turno que ya fue pagado.");
        _db.WorkShifts.Remove(shift);
        await _db.SaveChangesAsync(ct);
    }

    public async Task CreatePaymentAsync(int jobId, WorkPaymentCreateDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var job = await _db.IncomeSchedules.FirstOrDefaultAsync(s => s.Id == jobId && s.UserId == userId, ct)
            ?? throw new NotFoundException("El trabajo indicado no existe.");
        if (dto.Amount <= 0)
            throw new ValidationException("El monto debe ser mayor que cero.");

        _db.Incomes.Add(new Income
        {
            Amount = dto.Amount,
            Description = job.Name,
            Date = dto.Date.Date,
            Currency = job.Currency,
            PaymentMethodId = job.PaymentMethodId,
            IncomeScheduleId = job.Id,
            UserId = userId,
        });
        await _db.SaveChangesAsync(ct);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<Dictionary<int, List<WorkShift>>> LoadUnpostedShiftsAsync(List<int> jobIds, CancellationToken ct)
    {
        if (jobIds.Count == 0) return new();
        var shifts = await _db.WorkShifts
            .Where(w => w.IncomeId == null && jobIds.Contains(w.IncomeScheduleId))
            .ToListAsync(ct);
        return shifts.GroupBy(w => w.IncomeScheduleId).ToDictionary(g => g.Key, g => g.ToList());
    }

    private static PayType ParsePayType(string? value) =>
        string.Equals(value, "Hourly", StringComparison.OrdinalIgnoreCase) ? PayType.Hourly : PayType.Fixed;

    // Keep only valid #rrggbb hex; otherwise fall back to the app's default tint.
    private static string NormalizeColor(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (System.Text.RegularExpressions.Regex.IsMatch(raw, "^#[0-9a-fA-F]{6}$"))
            return raw.ToLowerInvariant();
        return "#0f5c4d";
    }

    private static PayFrequency ParsePayFrequency(string? value) => value?.ToLowerInvariant() switch
    {
        "weekly" => PayFrequency.Weekly,
        "biweekly" => PayFrequency.Biweekly,
        "semimonthly" => PayFrequency.SemiMonthly,
        _ => PayFrequency.Monthly,
    };

    /// <summary>
    /// Validates the schedule's timing fields against its frequency and returns the values to store.
    /// Weekly/Biweekly need an anchor date (day-of-month fields are ignored); Monthly needs the first
    /// day; SemiMonthly needs both days with the second greater than the first.
    /// </summary>
    private static (PayFrequency Frequency, int DayOfMonth, int? SecondDay, DateTime? Anchor) ValidateSchedule(
        string? frequencyValue, int? dayOfMonth, int? secondDay, DateTime? anchor)
    {
        var frequency = ParsePayFrequency(frequencyValue);

        if (frequency is PayFrequency.Weekly or PayFrequency.Biweekly)
        {
            if (anchor is null)
                throw new ValidationException("Indica la fecha de referencia de pago.");
            return (frequency, 1, null, anchor.Value.Date);
        }

        if (dayOfMonth is null or < 1 or > 31)
            throw new ValidationException("El día de pago debe estar entre 1 y 31.");

        if (frequency == PayFrequency.SemiMonthly)
        {
            if (secondDay is null or < 1 or > 31)
                throw new ValidationException("El segundo día de pago debe estar entre 1 y 31.");
            if (secondDay <= dayOfMonth)
                throw new ValidationException("El segundo día de pago debe ser mayor que el primero.");
            return (PayFrequency.SemiMonthly, dayOfMonth.Value, secondDay, null);
        }

        return (PayFrequency.Monthly, dayOfMonth.Value, null, null);
    }

    private static (decimal Amount, decimal? HourlyRate) ValidatePay(PayType payType, decimal amount, decimal? hourlyRate)
    {
        if (payType == PayType.Hourly)
        {
            if (hourlyRate is null or <= 0)
                throw new ValidationException("El valor por hora debe ser mayor que cero.");
            return (0m, hourlyRate);
        }
        if (amount <= 0)
            throw new ValidationException("El monto debe ser mayor que cero.");
        return (amount, null);
    }

    private async Task<PaymentMethod?> ResolvePaymentMethodAsync(int? id, string userId, CancellationToken ct)
    {
        if (id is null) return null;
        return await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct)
            ?? throw new NotFoundException("El medio de pago indicado no existe.");
    }

    private static IncomeScheduleDto Map(IncomeSchedule s, DateTime today) => new(
        s.Id, s.Name, s.PayType.ToString(), s.PayFrequency.ToString(), s.Amount, s.HourlyRate, s.Currency,
        s.Color,
        s.PaymentMethodId,
        s.PaymentMethod?.Name,
        s.PaymentMethod?.Type.ToString(),
        s.DayOfMonth, s.SecondDayOfMonth, s.AnchorDate, s.AutoPost, s.Active,
        IncomeSchedulePoster.NextPayDate(s, today),
        s.LastPostedPeriod);

    private static WorkShiftDto MapShift(WorkShift w) => new(
        w.Id, w.IncomeScheduleId, w.IncomeSchedule?.Name ?? string.Empty,
        w.Date, w.Hours, w.HourlyRate, w.Amount, w.Currency,
        w.IncomeId != null, w.IncomeId);
}
