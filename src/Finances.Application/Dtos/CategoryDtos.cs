using System.ComponentModel.DataAnnotations;

namespace Finances.Application.Dtos;

public record CategoryDto(
    int Id,
    string Name,
    string Icon,
    string Color,
    decimal? MonthlyBudget,
    bool IsSystem,
    IReadOnlyDictionary<string, decimal> Budgets);

public class CategoryCreateDto
{
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Icon { get; set; } = "tag";

    [MaxLength(9)]
    public string Color { get; set; } = "#6366f1";

    /// <summary>Budget amount for <see cref="BudgetCurrency"/> (defaults to the base currency).</summary>
    public decimal? MonthlyBudget { get; set; }

    /// <summary>Which currency the <see cref="MonthlyBudget"/> applies to. Null = user's base currency.</summary>
    [MaxLength(3)]
    public string? BudgetCurrency { get; set; }
}
