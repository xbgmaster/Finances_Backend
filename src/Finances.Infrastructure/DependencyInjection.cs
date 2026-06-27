using Finances.Application.Common;
using Finances.Application.Services;
using Finances.Infrastructure.Email;
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

        // Requerido por los token providers de Identity (reseteo de contrasena).
        services.AddDataProtection();

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
            .AddEntityFrameworkStores<FinanceDbContext>()
            .AddDefaultTokenProviders(); // necesario para tokens de reseteo de contrasena

        // Email delivery. Provider "smtp" (default) or "brevo" (HTTP API, works on
        // Render free tier where outbound SMTP ports are blocked). If not configured,
        // the sender just logs the message (useful for local development).
        var email = new EmailSettings
        {
            Provider = configuration["Email:Provider"] ?? "smtp",
            ApiKey = configuration["Email:ApiKey"] ?? string.Empty,
            Host = configuration["Email:Host"] ?? string.Empty,
            Port = int.TryParse(configuration["Email:Port"], out var p) ? p : 587,
            User = configuration["Email:User"] ?? string.Empty,
            Password = configuration["Email:Password"] ?? string.Empty,
            From = configuration["Email:From"] ?? "no-reply@finances.local",
            FromName = configuration["Email:FromName"] ?? "Finances",
            UseSsl = !bool.TryParse(configuration["Email:UseSsl"], out var ssl) || ssl,
            TimeoutSeconds = int.TryParse(configuration["Email:TimeoutSeconds"], out var ts) ? ts : 20,
        };
        services.AddSingleton(email);
        services.AddHttpClient();

        if (email.UsesBrevo)
            services.AddScoped<IEmailSender, BrevoEmailSender>();
        else
            services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Public URLs (used to build the password reset link).
        services.AddSingleton(new AppUrls
        {
            FrontendBaseUrl = configuration["App:FrontendUrl"] ?? "http://localhost:5173",
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        return services;
    }
}
