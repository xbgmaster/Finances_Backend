using System.ComponentModel.DataAnnotations;

namespace Finances.Application.Dtos;

public record IncomeDto(
    int Id, decimal Amount, string Description, DateTime Date, string Currency,
    int? PaymentMethodId = null, string? PaymentMethodName = null, string? PaymentMethodType = null,
    int? IncomeScheduleId = null);

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

    /// <summary>
    /// Job (income schedule) to attribute this income to. When null the current attribution is kept,
    /// so callers that don't manage jobs (Expenses/Dashboard) don't detach it accidentally.
    /// </summary>
    public int? IncomeScheduleId { get; set; }
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

// Server-side paginated expenses. Sum is the total amount across ALL matching rows
// (not just the current page), so the UI can show an accurate account/period subtotal.
public record PagedExpensesDto(
    IReadOnlyList<ExpenseDto> Items,
    int Total,
    int Page,
    int PageSize,
    decimal Sum);

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
