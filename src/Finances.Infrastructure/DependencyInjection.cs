using Finances.Application.Common;
using Finances.Application.Services;
using Finances.Infrastructure.Identity;
using Finances.Infrastructure.Persistence;
using Finances.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finances.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string webRootPath)
    {
        // Acepta DateTime con cualquier Kind (mapea a 'timestamp without time zone').
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'DefaultConnection'.");

        services.AddDbContext<FinanceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IFinanceDbContext>(sp => sp.GetRequiredService<FinanceDbContext>());

        // Almacenamiento de archivos (recibos).
        services.AddSingleton(new FileStorageOptions { RootPath = webRootPath });
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // JWT.
        var jwt = new JwtSettings
        {
            Key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Falta 'Jwt:Key'."),
            Issuer = configuration["Jwt:Issuer"] ?? "FinancesApi",
            Audience = configuration["Jwt:Audience"] ?? "FinancesClient",
            ExpiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var m) ? m : 480
        };
        services.AddSingleton(jwt);
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // Identity (autenticacion por correo/contrasena con roles).
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<FinanceDbContext>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        return services;
    }
}
