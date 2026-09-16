namespace Finances.Domain.Entities;

/// <summary>
/// A recurring income source (a "job"). It can either pay a fixed amount on a day of every month
/// (a salary) or be hourly: the user logs <see cref="WorkShift"/>s and the pay-cut day totals them.
/// When the pay day / pay-cut day arrives the system posts the matching <see cref="Income"/>
/// automatically, so the user doesn't have to enter it by hand.
/// </summary>
public class IncomeSchedule
{
    public int Id { get; set; }

    /// <summary>Display name of the job / income source (e.g. "Salary - Acme").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How this job pays: a fixed monthly amount or by logged hourly shifts.</summary>
    public PayType PayType { get; set; } = PayType.Fixed;

    /// <summary>Fixed amount paid every period. Used only when <see cref="PayType"/> is Fixed.</summary>
    public decimal Amount { get; set; }

    /// <summary>Default hourly rate for new shifts. Used only when <see cref="PayType"/> is Hourly.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>ISO currency code (e.g. "CAD"). Always set from the user's active currency lens.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Hex color used to tint this job's shifts/entries in the calendar (e.g. "#0f5c4d").</summary>
    public string? Color { get; set; }

    /// <summary>Account / payment method the income lands in (optional).</summary>
    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>How often the job pays within a month (monthly or twice a month).</summary>
    public PayFrequency PayFrequency { get; set; } = PayFrequency.Monthly;

    /// <summary>
    /// Day of the month the job pays / cuts pay (1-31, clamped to the month length when shorter).
    /// For hourly jobs this is the pay-cut day that totals the logged shifts.
    /// </summary>
    public int DayOfMonth { get; set; }

    /// <summary>
    /// Second pay / pay-cut day of the month, used only when <see cref="PayFrequency"/> is
    /// SemiMonthly. Clamped to the month length; falls back to the last day when not set.
    /// </summary>
    public int? SecondDayOfMonth { get; set; }

    /// <summary>
    /// Reference pay date for date-based frequencies (Weekly / Biweekly). It fixes both the weekday
    /// and, for Biweekly, the 14-day cycle. Unused for Monthly / SemiMonthly.
    /// </summary>
    public DateTime? AnchorDate { get; set; }

    /// <summary>When true, the pay is posted automatically once the pay day is reached.</summary>
    public bool AutoPost { get; set; } = true;

    /// <summary>When false, the schedule is paused (no auto-posting).</summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// Last period already posted, as "yyyyMM". Used to dedupe so each month's pay is posted once.
    /// Seeded on creation so past pay days are never back-posted.
    /// </summary>
    public string? LastPostedPeriod { get; set; }

    /// <summary>Owner (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
