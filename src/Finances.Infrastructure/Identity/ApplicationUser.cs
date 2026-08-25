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
    public string Currency { get; set; } = "CAD";
    public decimal? MonthlyIncomeTarget { get; set; }
    public bool OnboardingCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last time the user signed in. Null if they have never logged in.</summary>
    public DateTime? LastLoginAt { get; set; }
}
