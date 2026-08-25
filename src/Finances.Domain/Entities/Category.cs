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

    /// <summary>
    /// Budget in the user's base currency. Kept for backward compatibility; it mirrors the
    /// base-currency entry of <see cref="Budgets"/>.
    /// </summary>
    public decimal? MonthlyBudget { get; set; }

    /// <summary>
    /// Optional monthly budget per ISO currency, e.g. { "USD": 500, "COP": 1500000 }. Lets the
    /// same category have an independent budget in each currency (no conversion). Stored as jsonb.
    /// </summary>
    public Dictionary<string, decimal> Budgets { get; set; } = new();

    /// <summary>
    /// System-managed category that the user cannot edit or delete. Currently used only for
    /// the automatic expenses created when a credit payment is registered.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>Propietario de la categoria (Identity user id).</summary>
    public string UserId { get; set; } = string.Empty;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
