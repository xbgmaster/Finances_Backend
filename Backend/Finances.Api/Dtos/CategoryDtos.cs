using System.ComponentModel.DataAnnotations;

namespace Finances.Api.Dtos;

public record CategoryDto(int Id, string Name, string Icon, string Color, decimal? MonthlyBudget);

public class CategoryCreateDto
{
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Icon { get; set; } = "tag";

    [MaxLength(9)]
    public string Color { get; set; } = "#6366f1";

    public decimal? MonthlyBudget { get; set; }
}
