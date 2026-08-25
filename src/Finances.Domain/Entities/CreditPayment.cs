namespace Finances.Domain.Entities;

/// <summary>A single payment made against a <see cref="Credit"/> (an immutable ledger event).</summary>
public class CreditPayment
{
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>Optional note, e.g. "abono extraordinario a capital".</summary>
    public string? Note { get; set; }

    public int CreditId { get; set; }
    public Credit? Credit { get; set; }

    /// <summary>Owner of the payment (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;
}
