namespace Finances.Domain.Entities;

/// <summary>How often a job pays.</summary>
public enum PayFrequency
{
    /// <summary>Once a month, on <see cref="IncomeSchedule.DayOfMonth"/>.</summary>
    Monthly = 0,

    /// <summary>
    /// Twice a month ("quincena"): on <see cref="IncomeSchedule.DayOfMonth"/> and
    /// <see cref="IncomeSchedule.SecondDayOfMonth"/>.
    /// </summary>
    SemiMonthly = 1,

    /// <summary>Every 7 days, anchored to <see cref="IncomeSchedule.AnchorDate"/>.</summary>
    Weekly = 2,

    /// <summary>Every 14 days, anchored to <see cref="IncomeSchedule.AnchorDate"/>.</summary>
    Biweekly = 3,
}
