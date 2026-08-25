namespace Finances.Domain.Entities;

/// <summary>Gasto asociado a una categoria. Resta del saldo disponible del usuario.</summary>
public class Expense
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>URL relativa de la imagen del recibo/factura (opcional).</summary>
    public string? ReceiptUrl { get; set; }

    /// <summary>
    /// ISO currency code of this expense (e.g. "USD"). Null means the user's base currency.
    /// Foreign-currency expenses affect the balance of that currency, not the base one.
    /// </summary>
    public string? Currency { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>
    /// When set, this expense was auto-generated from a credit payment (an "abono/cuota").
    /// It is kept in sync with the payment and deleted automatically when the payment
    /// (or its parent credit) is removed. Null for regular, manually added expenses.
    /// </summary>
    public int? CreditPaymentId { get; set; }
    public CreditPayment? CreditPayment { get; set; }

    /// <summary>Propietario del gasto (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;
}
