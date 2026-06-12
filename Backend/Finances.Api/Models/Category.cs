using System.ComponentModel.DataAnnotations;

namespace Finances.Api.Models;

/// <summary>
/// Categoria de gasto (ej. Comida, Transporte) con su icono y color para la UI.
/// </summary>
public class Category
{
    public int Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Nombre/clave del icono que la UI mapea a un emoji o componente.</summary>
    [MaxLength(60)]
    public string Icon { get; set; } = "tag";

    /// <summary>Color en formato hex (#RRGGBB) usado en la UI.</summary>
    [MaxLength(9)]
    public string Color { get; set; } = "#6366f1";

    /// <summary>Presupuesto mensual opcional para esta categoria.</summary>
    public decimal? MonthlyBudget { get; set; }

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
