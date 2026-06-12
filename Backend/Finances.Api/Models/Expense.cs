using System.ComponentModel.DataAnnotations;

namespace Finances.Api.Models;

/// <summary>
/// Gasto asociado a una categoria. Resta del saldo disponible.
/// </summary>
public class Expense
{
    public int Id { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>URL relativa de la imagen del recibo/factura adjunta (opcional).</summary>
    [MaxLength(300)]
    public string? ReceiptUrl { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}
