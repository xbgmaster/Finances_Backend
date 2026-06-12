namespace Finances.Domain.Entities;

/// <summary>Ingreso de dinero. Suma al saldo disponible del usuario.</summary>
public class Income
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>Propietario del ingreso (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;
}
