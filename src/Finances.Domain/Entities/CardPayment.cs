namespace Finances.Domain.Entities;

/// <summary>
/// A payment applied to a revolving credit card. It reduces the card's outstanding debt
/// (freeing up cupo). Two flavours:
/// <list type="bullet">
/// <item>Funded from one of the user's cash/debit accounts (<see cref="SourcePaymentMethodId"/>
/// is set): the money leaves that account, so it also reduces the available cash balance.</item>
/// <item>External (<see cref="SourcePaymentMethodId"/> is null): money paid from outside the
/// tracked accounts. It only reduces the card debt, not the cash balance.</item>
/// </list>
/// The card debt and cash impact are NOT stored; they are derived from these payments.
/// </summary>
public class CardPayment
{
    public int Id { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>The credit card being paid down.</summary>
    public int CreditCardId { get; set; }
    public PaymentMethod? CreditCard { get; set; }

    /// <summary>Cash/debit account the money came from. Null means an external payment.</summary>
    public int? SourcePaymentMethodId { get; set; }
    public PaymentMethod? SourcePaymentMethod { get; set; }

    /// <summary>Amount paid, in the card's currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO currency code. Matches the card's currency.</summary>
    public string Currency { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Owner of the payment (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;
}
