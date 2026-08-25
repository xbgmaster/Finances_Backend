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
    string? Note);

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
}
