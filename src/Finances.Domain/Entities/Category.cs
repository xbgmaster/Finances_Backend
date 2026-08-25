namespace Finances.Domain.Entities;

/// <summary>Categoria de gasto con su icono, color y presupuesto opcional.</summary>
public class Category
{
    /// <summary>Name of the built-in system category that collects credit/loan payments.</summary>
    public const string DebtPaymentsSystemName = "Debt payments";

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "tag";
    public string Color { get; set; } = "#6366f1";
    public decimal? MonthlyBudget { get; set; }

    /// <summary>
    /// System-managed category that the user cannot edit or delete. Currently used only for
    /// the automatic expenses created when a credit payment is registered.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>Propietario de la categoria (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
