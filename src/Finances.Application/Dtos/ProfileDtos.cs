using System.ComponentModel.DataAnnotations;

namespace Finances.Application.Dtos;

public record UserProfileDto(
    string Id,
    string Email,
    string? FullName,
    string? Country,
    string Currency,
    decimal? MonthlyIncomeTarget,
    bool OnboardingCompleted,
    string Role);

public class UpdateProfileDto
{
    [MaxLength(120)]
    public string? FullName { get; set; }

    [MaxLength(80)]
    public string? Country { get; set; }

    [MaxLength(8)]
    public string Currency { get; set; } = "USD";

    public decimal? MonthlyIncomeTarget { get; set; }
}
