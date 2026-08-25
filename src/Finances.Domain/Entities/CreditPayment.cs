namespace Finances.Domain.Entities;

/// <summary>A single payment made against a <see cref="Credit"/> (an immutable ledger event).</summary>
public class CreditPayment
{
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this is a regular installment or an extra principal prepayment (abono a
    /// capital). Prepayments go entirely to principal and reshape the remaining plan.
    /// </summary>
    public CreditPaymentType Type { get; set; } = CreditPaymentType.Installment;

    /// <summary>
    /// For a principal prepayment, what the extra amount does to the remaining plan
    /// (reduce term vs reduce installment). Null for regular installments; when null on a
    /// prepayment the credit's default <see cref="Credit.PrepaymentEffect"/> is used.
    /// </summary>
    public PrepaymentEffect? Effect { get; set; }

    /// <summary>Optional note, e.g. "abono extraordinario a capital".</summary>
    public string? Note { get; set; }

    public int CreditId { get; set; }
    public Credit? Credit { get; set; }

    /// <summary>Owner of the payment (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;
}
