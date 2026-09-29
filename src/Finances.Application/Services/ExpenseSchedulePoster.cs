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

    /// <summary>Posts due subscription charges up to today. Does NOT save — caller does.</summary>
    public static int PostDue(IFinanceDbContext db, ExpenseSchedule schedule, DateTime today)
    {
        if (!schedule.Active || !schedule.AutoPost) return 0;
        if (schedule.Amount <= 0) return 0;

        if (!TryParseMarker(schedule.LastPostedPeriod, out var lastDate))
            lastDate = MostRecentOnOrBefore(schedule, today) ?? today;

        var posted = 0;
        foreach (var chargeDay in EnumerateChargeDates(schedule, lastDate, today))
        {
            var day = chargeDay.Date;
            var next = day.AddDays(1);
            var exists = db.Expenses.Local.Any(e =>
                    e.ExpenseScheduleId == schedule.Id
                    && e.UserId == schedule.UserId
                    && e.Date >= day && e.Date < next)
                || db.Expenses.Any(e =>
                    e.ExpenseScheduleId == schedule.Id
                    && e.UserId == schedule.UserId
                    && e.Date >= day && e.Date < next);
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
