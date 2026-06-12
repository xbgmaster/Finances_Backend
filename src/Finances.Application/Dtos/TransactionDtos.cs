using System.ComponentModel.DataAnnotations;

namespace Finances.Application.Dtos;

public record IncomeDto(int Id, decimal Amount, string Description, DateTime Date);

public class IncomeCreateDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public DateTime? Date { get; set; }
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
    string? ReceiptUrl);

public class ExpenseCreateDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public DateTime? Date { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una categoria valida.")]
    public int CategoryId { get; set; }
}
