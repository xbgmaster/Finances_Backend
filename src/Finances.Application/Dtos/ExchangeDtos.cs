using System.ComponentModel.DataAnnotations;

namespace Finances.Application.Dtos;

public record ExchangeDto(
    int Id,
    DateTime Date,
    string FromCurrency,
    decimal FromAmount,
    string ToCurrency,
    decimal ToAmount,
    decimal Rate,
    string? Note,
    int? FromPaymentMethodId = null,
    string? FromPaymentMethodName = null,
    int? ToPaymentMethodId = null,
    string? ToPaymentMethodName = null);

public class ExchangeCreateDto
{
    [Required, MaxLength(3)]
    public string FromCurrency { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal FromAmount { get; set; }

    [Required, MaxLength(3)]
    public string ToCurrency { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal ToAmount { get; set; }

    public DateTime? Date { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }

    /// <summary>Cash/debit account the money leaves from (source currency). Optional.</summary>
    public int? FromPaymentMethodId { get; set; }

    /// <summary>Cash/debit account the money lands in (destination currency). Optional;
    /// if omitted, it defaults to the destination currency's favorite/first cash account.</summary>
    public int? ToPaymentMethodId { get; set; }
}
