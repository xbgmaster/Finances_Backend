using Microsoft.AspNetCore.Identity;

namespace Finances.Infrastructure.Identity;

/// <summary>
/// Usuario de la aplicacion. Extiende IdentityUser con los datos de perfil
/// que se completan en el modulo de onboarding.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? Country { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal? MonthlyIncomeTarget { get; set; }
    public bool OnboardingCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
