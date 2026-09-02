namespace Finances.Domain.Entities;

/// <summary>
/// A payment method / account the user pays with: a debit card, cash, or a revolving
/// credit card. Transactions can reference one so the user can track spending per card
/// and, for credit cards, how much of the credit limit (cupo) is left.
/// The balance and available credit are NOT stored: they are derived from the linked
/// expenses (and, later, card payments).
/// </summary>
public class PaymentMethod
{
    /// <summary>Name of the built-in cash account seeded for every user.</summary>
    public const string DefaultCashName = "Efectivo";

    /// <summary>
    /// Default cash account created for each user so an expense always has a method to pick
    /// (payment method is required on expenses). Currency is null so it follows the base one.
    /// </summary>
    public static PaymentMethod DefaultCash(string userId) => new()
    {
        Name = DefaultCashName,
        Type = PaymentMethodType.Cash,
        Color = "#10b981",
        Icon = "cash",
        UserId = userId
    };

    public int Id { get; set; }

    /// <summary>Friendly name, e.g. "Visa CIBC" or "Debito Scotiabank".</summary>
    public string Name { get; set; } = string.Empty;

    public PaymentMethodType Type { get; set; } = PaymentMethodType.Debit;

    /// <summary>ISO currency code (optional). Null means the user's base currency.</summary>
    public string? Currency { get; set; }

    public string Color { get; set; } = "#6366f1";
    public string Icon { get; set; } = "card";

    /// <summary>Credit limit (cupo total). Only meaningful for <see cref="PaymentMethodType.CreditCard"/>.</summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>Day of the month (1-31) the statement closes. Credit cards only (optional).</summary>
    public int? StatementDay { get; set; }

    /// <summary>Day of the month (1-31) the payment is due. Credit cards only (optional).</summary>
    public int? PaymentDueDay { get; set; }

    /// <summary>
    /// Tracks the last due-date reminder sent for this card (see the credit reminder pattern).
    /// Reserved for the card due-date alerts added in a later phase.
    /// </summary>
    public string? LastReminderKey { get; set; }

    /// <summary>Archived methods are hidden from pickers but kept for historical reports.</summary>
    public bool Archived { get; set; }

    /// <summary>
    /// The user's preferred method. At most one per user; it is listed first and
    /// preselected when adding an expense.
    /// </summary>
    public bool IsFavorite { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Owner of the payment method (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;
}
