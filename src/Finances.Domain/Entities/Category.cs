namespace Finances.Domain.Entities;

/// <summary>Categoria de gasto con su icono, color y presupuesto opcional.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "tag";
    public string Color { get; set; } = "#6366f1";
    public decimal? MonthlyBudget { get; set; }

    /// <summary>Propietario de la categoria (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
