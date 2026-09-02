namespace Finances.Domain.Entities;

/// <summary>Ingreso de dinero. Suma al saldo disponible del usuario.</summary>
public class Income
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ISO currency code of this income (e.g. "USD"). Null means the user's base currency.
    /// Enables holding balances in more than one currency.
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Account / payment method the income landed in (optional). Null means "unspecified".
    /// </summary>
    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Propietario del ingreso (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;
}
