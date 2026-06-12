using System.ComponentModel.DataAnnotations;

namespace Finances.Api.Models;

/// <summary>
/// Ingreso de dinero (salario, venta, etc.). Suma al saldo disponible.
/// </summary>
public class Income
{
    public int Id { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.UtcNow;
}
