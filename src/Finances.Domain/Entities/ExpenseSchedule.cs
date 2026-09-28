namespace Finances.Domain.Entities;

/// <summary>
/// A recurring expense ("subscription") charged on a fixed day each month (or twice a month).
/// When the charge day arrives and AutoPost is on, the system posts a matching <see cref="Expense"/>
/// against the linked payment method (debit or credit), so Available balance / card debt update.
/// </summary>
public class ExpenseSchedule
{
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Netflix"). Used as the expense description when posted.</summary>
    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>ISO currency code (e.g. "CAD").</summary>
    public string Currency { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Card / account the charge hits (debit or credit). Required.</summary>
    public int PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    public PayFrequency PayFrequency { get; set; } = PayFrequency.Monthly;

    /// <summary>Day of month the charge posts (1–31, clamped).</summary>
    public int DayOfMonth { get; set; }

    /// <summary>Second charge day when <see cref="PayFrequency"/> is SemiMonthly.</summary>
    public int? SecondDayOfMonth { get; set; }

    public bool AutoPost { get; set; } = true;
    public bool Active { get; set; } = true;

    /// <summary>Last posted charge date as yyyyMMdd (idempotent auto-post).</summary>
    public string? LastPostedPeriod { get; set; }

    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
