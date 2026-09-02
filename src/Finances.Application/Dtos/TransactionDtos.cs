using System.ComponentModel.DataAnnotations;

namespace Finances.Application.Dtos;

public record IncomeDto(
    int Id, decimal Amount, string Description, DateTime Date, string Currency,
    int? PaymentMethodId = null, string? PaymentMethodName = null, string? PaymentMethodType = null);

public class IncomeCreateDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public DateTime? Date { get; set; }

    /// <summary>ISO currency code. Empty/null falls back to the user's base currency.</summary>
    [MaxLength(3)]
    public string? Currency { get; set; }

    /// <summary>Account / payment method the income landed in (optional).</summary>
    public int? PaymentMethodId { get; set; }
}

public class IncomeUpdateDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTime? Date { get; set; }

    /// <summary>ISO currency code. Empty/null falls back to the user's base currency.</summary>
    [MaxLength(3)]
    public string? Currency { get; set; }

    /// <summary>Account / payment method the income landed in (optional).</summary>
    public int? PaymentMethodId { get; set; }
}

public record ExpenseDto(
    int Id,
    decimal Amount,
    string Description,
    DateTime Date,
    int CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    string? ReceiptUrl,
    string Currency,
    // When set, this expense mirrors a credit payment and must be managed from that credit.
    int? CreditId = null,
    // Payment method / card used (optional), for per-card tracking.
    int? PaymentMethodId = null,
    string? PaymentMethodName = null,
    string? PaymentMethodType = null);

public class ExpenseCreateDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTime? Date { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una categoria valida.")]
    public int CategoryId { get; set; }

    /// <summary>ISO currency code. Empty/null falls back to the user's base currency.</summary>
    [MaxLength(3)]
    public string? Currency { get; set; }

    /// <summary>Payment method / card used to pay (optional).</summary>
    public int? PaymentMethodId { get; set; }
}

public class ExpenseUpdateDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    // Nullable so an empty description is valid (no implicit "required" from non-nullable strings).
    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTime? Date { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una categoria valida.")]
    public int CategoryId { get; set; }

    /// <summary>ISO currency code. Empty/null falls back to the user's base currency.</summary>
    [MaxLength(3)]
    public string? Currency { get; set; }

    /// <summary>Payment method / card used to pay (optional).</summary>
    public int? PaymentMethodId { get; set; }

    /// <summary>Remove the existing receipt image (ignored when a new receipt file is uploaded).</summary>
    public bool RemoveReceipt { get; set; }
}
