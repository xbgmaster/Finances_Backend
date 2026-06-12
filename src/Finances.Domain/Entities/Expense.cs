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

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Propietario del gasto (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;
}
