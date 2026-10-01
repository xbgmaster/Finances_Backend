namespace Finances.Domain.Entities;

/// <summary>
/// Overrides the amount of a single scheduled pay occurrence (one pay day of a fixed-salary job).
/// Created when the user "adjusts" a scheduled payment — the occurrence stays as SCHEDULED in the
/// calendar and is posted automatically with this amount instead of the job's default Amount.
/// </summary>
public class PayOccurrenceOverride
{
    public int Id { get; set; }
    public int IncomeScheduleId { get; set; }
    public IncomeSchedule? IncomeSchedule { get; set; }

    /// <summary>The specific pay date this override applies to (date-only, UTC midnight).</summary>
    public DateTime PayDate { get; set; }

    /// <summary>Overridden amount for this occurrence.</summary>
    public decimal Amount { get; set; }

    /// <summary>Optional note shown on the income when this occurrence is posted.</summary>
    public string? Description { get; set; }

    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
