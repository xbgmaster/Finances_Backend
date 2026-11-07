using System.Globalization;
using Finances.Application.Common;
using Finances.Domain.Entities;

namespace Finances.Application.Services;

/// <summary>
/// Posts due <see cref="Expense"/> rows from recurring <see cref="ExpenseSchedule"/>s.
/// Monthly / SemiMonthly only (subscriptions). Idempotent via LastPostedPeriod (yyyyMMdd).
/// </summary>
public static class ExpenseSchedulePoster
{
    private static (int Year, int Month) NextYM(int year, int month) =>
        month >= 12 ? (year + 1, 1) : (year, month + 1);

    private static (int Year, int Month) PrevYM(int year, int month) =>
        month <= 1 ? (year - 1, 12) : (year, month - 1);

    private static DateTime ClampDay(int year, int month, int day)
    {
        var dim = DateTime.DaysInMonth(year, month);
        return new DateTime(year, month, Math.Min(Math.Max(1, day), dim));
    }

    public static IReadOnlyList<DateTime> ChargeDatesInMonth(ExpenseSchedule s, int year, int month)
    {
        var days = new List<DateTime> { ClampDay(year, month, s.DayOfMonth) };
        if (s.PayFrequency == PayFrequency.SemiMonthly)
        {
            var second = s.SecondDayOfMonth ?? DateTime.DaysInMonth(year, month);
            days.Add(ClampDay(year, month, second));
        }
        return days.Distinct().OrderBy(d => d).ToList();
    }

    public static DateTime NextChargeDate(ExpenseSchedule s, DateTime today)
    {
        foreach (var d in ChargeDatesInMonth(s, today.Year, today.Month))
            if (d.Date >= today.Date) return d;
        var (y, m) = NextYM(today.Year, today.Month);
        return ChargeDatesInMonth(s, y, m)[0];
    }

    private static DateTime? MostRecentOnOrBefore(ExpenseSchedule s, DateTime today)
    {
        DateTime? last = null;
        foreach (var d in ChargeDatesInMonth(s, today.Year, today.Month))
            if (d.Date <= today.Date) last = d;
        if (last is not null) return last;
        var (py, pm) = PrevYM(today.Year, today.Month);
        return ChargeDatesInMonth(s, py, pm)[^1];
    }

    private static DateTime? MostRecentBefore(ExpenseSchedule s, DateTime today)
    {
        var onOrBefore = MostRecentOnOrBefore(s, today);
        if (onOrBefore is null) return null;
        if (onOrBefore.Value.Date < today.Date) return onOrBefore;
        return MostRecentOnOrBefore(s, today.AddDays(-1));
    }

    public static string SeedLastPostedPeriod(ExpenseSchedule s, DateTime today)
    {
        var last = MostRecentBefore(s, today);
        if (last is not null) return last.Value.ToString("yyyyMMdd");
        var next = NextChargeDate(s, today);
        return next.AddDays(-1).ToString("yyyyMMdd");
    }

    private static bool TryParseMarker(string? marker, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(marker)) return false;
        if (marker.Length == 8 &&
            DateTime.TryParseExact(marker, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        if (marker.Length == 6 && int.TryParse(marker[..4], out var y) && int.TryParse(marker[4..], out var m)
            && m is >= 1 and <= 12)
        {
            date = new DateTime(y, m, DateTime.DaysInMonth(y, m));
            return true;
        }
        return false;
    }

    private static IEnumerable<DateTime> EnumerateChargeDates(ExpenseSchedule s, DateTime afterExclusive, DateTime until)
    {
        var (y, m) = (afterExclusive.Year, afterExclusive.Month);
        while (y < until.Year || (y == until.Year && m <= until.Month))
        {
            foreach (var charge in ChargeDatesInMonth(s, y, m))
                if (charge.Date > afterExclusive.Date && charge.Date <= until.Date)
                    yield return charge;
            (y, m) = NextYM(y, m);
        }
    }

    /// <summary>
    /// The charge day this expense belongs to, even if the user moved it earlier or later in the month.
    /// </summary>
    public static DateTime OccurrenceDate(ExpenseSchedule schedule, DateTime expenseDate)
    {
        var date = expenseDate.Date;
        var candidates = new List<DateTime>();
        for (var offset = -1; offset <= 1; offset++)
        {
            var shifted = date.AddMonths(offset);
            candidates.AddRange(ChargeDatesInMonth(schedule, shifted.Year, shifted.Month));
        }
        return candidates
            .OrderBy(d => Math.Abs((d.Date - date).TotalDays))
            .ThenBy(d => d)
            .First();
    }

    /// <summary>
    /// Marks the occurrence already handled. A later edit, move, or delete of that month
    /// must not post the charge again. Later months still post.
    /// </summary>
    public static void AcknowledgeOccurrence(ExpenseSchedule schedule, DateTime expenseDate)
    {
        var day = OccurrenceDate(schedule, expenseDate);
        if (!TryParseMarker(schedule.LastPostedPeriod, out var last) || last.Date < day)
            schedule.LastPostedPeriod = day.ToString("yyyyMMdd");
    }

    private static bool CoversCharge(ExpenseSchedule schedule, DateTime expenseDate, DateTime chargeDay) =>
        OccurrenceDate(schedule, expenseDate).Date == chargeDay.Date;

    private static bool ChargeExists(IFinanceDbContext db, ExpenseSchedule schedule, DateTime chargeDay)
    {
        bool Covers(DateTime expenseDate) => CoversCharge(schedule, expenseDate, chargeDay);
        if (db.Expenses.Local.Any(e =>
                e.ExpenseScheduleId == schedule.Id
                && e.UserId == schedule.UserId
                && Covers(e.Date)))
            return true;

        var dates = db.Expenses
            .Where(e => e.ExpenseScheduleId == schedule.Id && e.UserId == schedule.UserId)
            .Select(e => e.Date)
            .ToList();
        return dates.Any(Covers);
    }

    /// <summary>Posts due subscription charges up to today. Does NOT save — caller does.</summary>
    public static int PostDue(IFinanceDbContext db, ExpenseSchedule schedule, DateTime today)
    {
        if (!schedule.Active || !schedule.AutoPost) return 0;
        if (schedule.Amount <= 0) return 0;

        if (!TryParseMarker(schedule.LastPostedPeriod, out var lastDate))
            lastDate = MostRecentOnOrBefore(schedule, today) ?? today;
        // A charge the user deleted stays deleted. The marker already passed that day,
        // so it is not posted again. Later charge dates still post normally.

        var posted = 0;
        foreach (var chargeDay in EnumerateChargeDates(schedule, lastDate, today))
        {
            var exists = ChargeExists(db, schedule, chargeDay);
            if (exists)
            {
                schedule.LastPostedPeriod = chargeDay.ToString("yyyyMMdd");
                continue;
            }

            db.Expenses.Add(new Expense
            {
                Amount = schedule.Amount,
                Description = schedule.Name,
                Date = chargeDay,
                Currency = schedule.Currency,
                CategoryId = schedule.CategoryId,
                PaymentMethodId = schedule.PaymentMethodId,
                ExpenseScheduleId = schedule.Id,
                UserId = schedule.UserId,
            });
            schedule.LastPostedPeriod = chargeDay.ToString("yyyyMMdd");
            posted++;
        }
        return posted;
    }
}
