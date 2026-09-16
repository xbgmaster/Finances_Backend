namespace Finances.Domain.Entities;

/// <summary>
/// A single work shift logged against an hourly <see cref="IncomeSchedule"/> ("job"). Its amount is
/// hours × rate. Shifts accumulate until the job's pay-cut day, when all unposted shifts up to that
/// day are totaled into one <see cref="Income"/> (linked via <see cref="IncomeId"/>).
/// </summary>
public class WorkShift
{
    public int Id { get; set; }

    /// <summary>The hourly job this shift belongs to.</summary>
    public int IncomeScheduleId { get; set; }
    public IncomeSchedule? IncomeSchedule { get; set; }

    /// <summary>Day the shift was worked.</summary>
    public DateTime Date { get; set; }

    /// <summary>Hours worked in the shift.</summary>
    public decimal Hours { get; set; }

    /// <summary>Rate applied to this shift (snapshot; defaults to the job's rate but editable).</summary>
    public decimal HourlyRate { get; set; }

    /// <summary>Computed earnings for the shift (Hours × HourlyRate), stored for fast totals.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO currency code, inherited from the job.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// When set, this shift has already been totaled into a posted income (pay cut done). Nulled if
    /// that income is deleted, so the shift can be swept into the next cut again.
    /// </summary>
    public int? IncomeId { get; set; }
    public Income? Income { get; set; }

    /// <summary>Owner (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
