using System.Globalization;
using Finances.Application.Common;
using Finances.Domain.Entities;

namespace Finances.Application.Services;

/// <summary>
/// Pure logic that turns a recurring <see cref="IncomeSchedule"/> into concrete <see cref="Income"/>
/// rows when each pay day is reached. Frequency-aware (monthly or twice a month) and idempotent:
/// it advances <see cref="IncomeSchedule.LastPostedPeriod"/> (stored as the last posted pay date,
/// yyyyMMdd) so a pay day is never posted twice. Shared by the per-user catch-up and the background
/// auto-post job so both behave identically.
/// </summary>
public static class IncomeSchedulePoster
{
    private static (int Year, int Month) NextYM(int year, int month) =>
        month >= 12 ? (year + 1, 1) : (year, month + 1);

    private static (int Year, int Month) PrevYM(int year, int month) =>
        month <= 1 ? (year - 1, 12) : (year, month - 1);

    /// <summary>A day clamped to the given month's length (e.g. 31 -> 28/30).</summary>
    private static DateTime ClampDay(int year, int month, int day)
    {
        var dim = DateTime.DaysInMonth(year, month);
        return new DateTime(year, month, Math.Min(Math.Max(1, day), dim));
    }

    /// <summary>The pay days of a schedule within a month, ascending and deduped.</summary>
    public static IReadOnlyList<DateTime> PayDatesInMonth(IncomeSchedule s, int year, int month)
    {
        var days = new List<DateTime> { ClampDay(year, month, s.DayOfMonth) };
        if (s.PayFrequency == PayFrequency.SemiMonthly)
        {
            var second = s.SecondDayOfMonth ?? DateTime.DaysInMonth(year, month);
            days.Add(ClampDay(year, month, second));
        }
        return days.Distinct().OrderBy(d => d).ToList();
    }

    /// <summary>Step in days for the date-based frequencies (weekly / every two weeks).</summary>
    private static int StepDays(IncomeSchedule s) =>
        s.PayFrequency == PayFrequency.Weekly ? 7 : s.PayFrequency == PayFrequency.Biweekly ? 14 : 0;

    /// <summary>True for frequencies anchored to a date/weekday rather than a day of the month.</summary>
    private static bool IsDateBased(IncomeSchedule s) =>
        s.PayFrequency is PayFrequency.Weekly or PayFrequency.Biweekly;

    /// <summary>Monday–Friday days after <paramref name="start"/>. Zero returns the start date.</summary>
    public static DateTime AddBusinessDays(DateTime start, int businessDays)
    {
        var date = start.Date;
        var left = businessDays;
        while (left > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                left--;
        }
        return date;
    }

    /// <summary>Day the money arrives for a pay cut. No lag means the cut date itself.</summary>
    public static DateTime PaymentDate(IncomeSchedule s, DateTime cut)
    {
        var lag = s.PaymentLagBusinessDays;
        return lag <= 0 ? cut.Date : AddBusinessDays(cut, lag);
    }

    /// <summary>First day covered by the pay cut (the day after the previous cut).</summary>
    public static DateTime PeriodStart(IncomeSchedule s, DateTime cut)
    {
        var step = StepDays(s);
        if (step > 0) return cut.Date.AddDays(1 - step);
        var prev = MostRecentPayBefore(s, cut);
        return prev is null ? new DateTime(cut.Year, cut.Month, 1) : prev.Value.Date.AddDays(1);
    }

    /// <summary>The next cut date on or after <paramref name="today"/>.</summary>
    public static DateTime NextCutDate(IncomeSchedule s, DateTime today)
    {
        if (IsDateBased(s))
        {
            var step = StepDays(s);
            var anchor = (s.AnchorDate ?? today).Date;
            if (anchor >= today.Date) return anchor;
            var k = ((today.Date - anchor).Days + step - 1) / step; // ceil
            return anchor.AddDays(k * step);
        }
        foreach (var d in PayDatesInMonth(s, today.Year, today.Month))
            if (d.Date >= today.Date) return d;
        var (y, m) = NextYM(today.Year, today.Month);
        return PayDatesInMonth(s, y, m)[0];
    }

    static DateTime NextCutAfter(IncomeSchedule s, DateTime cut)
    {
        var step = StepDays(s);
        if (step > 0) return cut.Date.AddDays(step);
        return NextCutDate(s, cut.Date.AddDays(1));
    }

    /// <summary>The next day the money arrives, on or after <paramref name="today"/>.</summary>
    public static DateTime NextPayDate(IncomeSchedule s, DateTime today)
    {
        var cut = MostRecentPayOnOrBefore(s, today) ?? NextCutDate(s, today);
        for (var i = 0; i < 24; i++)
        {
            var pay = PaymentDate(s, cut);
            if (pay.Date >= today.Date) return pay;
            cut = NextCutAfter(s, cut);
        }
        return PaymentDate(s, cut);
    }

    /// <summary>The most recent pay date on or before today, or null if the first pay is still ahead.</summary>
    private static DateTime? MostRecentPayOnOrBefore(IncomeSchedule s, DateTime today)
    {
        if (IsDateBased(s))
        {
            var step = StepDays(s);
            var anchor = (s.AnchorDate ?? today).Date;
            if (anchor > today.Date) return null;
            var k = (today.Date - anchor).Days / step;
            return anchor.AddDays(k * step);
        }
        DateTime? last = null;
        foreach (var d in PayDatesInMonth(s, today.Year, today.Month))
            if (d.Date <= today.Date) last = d;
        if (last is not null) return last;
        var (py, pm) = PrevYM(today.Year, today.Month);
        return PayDatesInMonth(s, py, pm)[^1];
    }

    /// <summary>The most recent pay date strictly before today (excludes today itself), or null.</summary>
    private static DateTime? MostRecentPayBefore(IncomeSchedule s, DateTime today)
    {
        var onOrBefore = MostRecentPayOnOrBefore(s, today);
        if (onOrBefore is null) return null;
        if (onOrBefore.Value.Date < today.Date) return onOrBefore;
        // It falls exactly on today: step back one pay period so today can still be posted.
        return MostRecentPayOnOrBefore(s, today.AddDays(-1));
    }

    /// <summary>
    /// Baseline marker to store on creation. We seed with the most recent pay date *strictly before*
    /// today, so a job created on its pay day still posts today's pay, while past pay days (created
    /// after the fact) are never back-posted. If the first pay is still ahead, the slot right before
    /// it. Stored as yyyyMMdd.
    /// </summary>
    public static string SeedLastPostedPeriod(IncomeSchedule s, DateTime today)
    {
        var last = MostRecentPayBefore(s, today);
        if (last is not null) return last.Value.ToString("yyyyMMdd");
        var next = NextPayDate(s, today);
        var step = StepDays(s);
        var before = step > 0 ? next.AddDays(-step) : next.AddDays(-1);
        return before.ToString("yyyyMMdd");
    }

    private static bool TryParseMarker(string? marker, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(marker)) return false;
        if (marker.Length == 8 &&
            DateTime.TryParseExact(marker, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        // Back-compat with the old yyyyMM marker: treat it as "posted through that month".
        if (marker.Length == 6 && int.TryParse(marker[..4], out var y) && int.TryParse(marker[4..], out var m)
            && m is >= 1 and <= 12)
        {
            date = new DateTime(y, m, DateTime.DaysInMonth(y, m));
            return true;
        }
        return false;
    }

    /// <summary>Pay dates strictly after <paramref name="afterExclusive"/> and on/before <paramref name="until"/>, in order.</summary>
    private static IEnumerable<DateTime> EnumeratePayDates(IncomeSchedule s, DateTime afterExclusive, DateTime until)
    {
        if (IsDateBased(s))
        {
            var step = StepDays(s);
            var anchor = (s.AnchorDate ?? until).Date;
            var d = anchor;
            if (d.Date <= afterExclusive.Date)
            {
                var k = (afterExclusive.Date - anchor.Date).Days / step + 1;
                d = anchor.AddDays(k * step);
            }
            while (d.Date <= afterExclusive.Date) d = d.AddDays(step); // safety
            while (d.Date <= until.Date)
            {
                yield return d;
                d = d.AddDays(step);
            }
            yield break;
        }

        var (y, m) = (afterExclusive.Year, afterExclusive.Month);
        while (y < until.Year || (y == until.Year && m <= until.Month))
        {
            foreach (var pay in PayDatesInMonth(s, y, m))
                if (pay.Date > afterExclusive.Date && pay.Date <= until.Date)
                    yield return pay;
            (y, m) = NextYM(y, m);
        }
    }

    /// <summary>
    /// Posts due income up to <paramref name="today"/> and advances
    /// <see cref="IncomeSchedule.LastPostedPeriod"/>. Does NOT save — the caller does.
    /// For fixed jobs it posts the fixed amount on each reached pay day; for hourly jobs it totals
    /// the unposted <paramref name="shifts"/> in the pay window (previous cut, this cut] into one income.
    /// Returns the number of incomes posted.
    /// </summary>
    public static int PostDue(IFinanceDbContext db, IncomeSchedule schedule, DateTime today,
        IReadOnlyList<WorkShift>? shifts = null,
        IReadOnlyList<PayOccurrenceOverride>? overrides = null)
    {
        if (!schedule.Active || !schedule.AutoPost) return 0;

        if (!TryParseMarker(schedule.LastPostedPeriod, out var lastDate))
            lastDate = MostRecentPayOnOrBefore(schedule, today) ?? today;
        // A pay the user already has, moved, or deleted stays that way.
        // Opening Payroll again must not create a second income for the same cut.

        AlignHourlyIncome(db, schedule, shifts, today);

        var posted = 0;
        foreach (var payDay in EnumeratePayDates(schedule, lastDate, today))
        {
            var deposit = PaymentDate(schedule, payDay);
            // The cut can close before the money arrives. Wait, and do not skip this cut.
            if (deposit.Date > today.Date) break;
            posted += schedule.PayType == PayType.Hourly
                ? PostHourlyCut(db, schedule, payDay, deposit, shifts)
                : PostFixed(db, schedule, payDay, deposit, overrides);
            // The marker stays on the cut, so a later change of the payment delay
            // does not create another income for a cut already posted.
            schedule.LastPostedPeriod = payDay.ToString("yyyyMMdd");
        }
        return posted;
    }

    private static int PostFixed(IFinanceDbContext db, IncomeSchedule schedule, DateTime payDay, DateTime deposit,
        IReadOnlyList<PayOccurrenceOverride>? overrides = null)
    {
        // Idempotent against concurrent post-due (UI) + background job races.
        if (AlreadyPosted(db, schedule, payDay, deposit)) return 0;

        // Use overridden amount if the user adjusted this specific occurrence.
        var ov = overrides?.FirstOrDefault(o => o.PayDate.Date == payDay.Date);
        var amount = ov?.Amount ?? schedule.Amount;
        if (amount <= 0) return 0;
        db.Incomes.Add(new Income
        {
            Amount = amount,
            Description = string.IsNullOrWhiteSpace(ov?.Description) ? schedule.Name : ov!.Description!.Trim(),
            Date = deposit,
            PayCutDate = payDay.Date,
            Currency = schedule.Currency,
            PaymentMethodId = schedule.PaymentMethodId,
            IncomeScheduleId = schedule.Id,
            UserId = schedule.UserId,
        });
        return 1;
    }

    private static bool CoversCut(Income i, IncomeSchedule schedule, DateTime cut, DateTime deposit) =>
        i.IncomeScheduleId == schedule.Id
        && i.UserId == schedule.UserId
        && (i.PayCutDate?.Date == cut.Date || i.Date.Date == cut.Date || i.Date.Date == deposit.Date);

    private static bool AlreadyPosted(IFinanceDbContext db, IncomeSchedule schedule, DateTime payDay, DateTime deposit)
    {
        if (db.Incomes.Local.Any(i => CoversCut(i, schedule, payDay, deposit)))
            return true;
        var cut = payDay.Date;
        var paid = deposit.Date;
        return db.Incomes.Any(i =>
            i.IncomeScheduleId == schedule.Id
            && i.UserId == schedule.UserId
            && ((i.PayCutDate != null && i.PayCutDate.Value.Date == cut)
                || i.Date.Date == cut
                || i.Date.Date == paid));
    }

    /// <summary>
    /// The posted paycheck is the sum of each shift's own amount. A shift can use a
    /// different hourly rate (holiday, overtime) than the job's usual rate.
    /// </summary>
    private static void AlignHourlyIncome(IFinanceDbContext db, IncomeSchedule schedule, IReadOnlyList<WorkShift>? shifts, DateTime today)
    {
        if (schedule.PayType != PayType.Hourly || shifts is null || shifts.Count == 0) return;
        var cut = MostRecentPayOnOrBefore(schedule, today);
        if (cut is null) return;
        var deposit = PaymentDate(schedule, cut.Value);
        if (deposit.Date > today.Date) return;

        var paid = deposit.Date;
        var cutDate = cut.Value.Date;
        var income = db.Incomes.Local.FirstOrDefault(i => CoversCut(i, schedule, cutDate, paid))
            ?? db.Incomes.FirstOrDefault(i =>
                i.IncomeScheduleId == schedule.Id
                && i.UserId == schedule.UserId
                && ((i.PayCutDate != null && i.PayCutDate.Value.Date == cutDate)
                    || i.Date.Date == cutDate
                    || i.Date.Date == paid));
        if (income is null) return;

        var prev = MostRecentPayBefore(schedule, cut.Value);
        var window = shifts
            .Where(w => w.Date.Date <= cutDate && (prev is null || w.Date.Date > prev.Value.Date))
            .ToList();
        if (window.Count == 0) return;
        var total = window.Sum(w => w.Amount);
        if (income.Amount != total) income.Amount = total;
    }

    private static int PostHourlyCut(IFinanceDbContext db, IncomeSchedule schedule, DateTime payDay, DateTime deposit, IReadOnlyList<WorkShift>? shifts)
    {
        if (shifts is null || shifts.Count == 0) return 0;
        if (AlreadyPosted(db, schedule, payDay, deposit)) return 0;

        // Only shifts in this pay window: (previousCut, payDay]. Sweeping every unposted
        // shift on/before payDay pulled older periods into a single income (e.g. 679 vs 558).
        var prevCut = MostRecentPayBefore(schedule, payDay);

        var due = shifts
            .Where(w => w.IncomeId == null && w.Income == null
                && w.Date.Date <= payDay.Date
                && (prevCut is null || w.Date.Date > prevCut.Value.Date))
            .ToList();
        if (due.Count == 0) return 0;

        var total = due.Sum(w => w.Amount);
        if (total <= 0) return 0;

        var income = new Income
        {
            Amount = total,
            Description = schedule.Name,
            Date = deposit,
            PayCutDate = payDay.Date,
            Currency = schedule.Currency,
            PaymentMethodId = schedule.PaymentMethodId,
            IncomeScheduleId = schedule.Id,
            UserId = schedule.UserId,
        };
        db.Incomes.Add(income);
        foreach (var w in due) w.Income = income; // FK set on save
        return 1;
    }
}
