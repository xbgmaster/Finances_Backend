using System.Text;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Finances.Api.Auth;
using Finances.Api.Middleware;
using Finances.Application;
using Finances.Application.Common;
using Finances.Infrastructure;
using Finances.Infrastructure.Identity;
using Finances.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Load secrets (connection string, JWT key, SMTP password) from Azure Key Vault.
// Secret names use "--" to map to config sections, e.g. "ConnectionStrings--DefaultConnection".
// Auth uses DefaultAzureCredential: a Service Principal via env vars on Render
// (AZURE_TENANT_ID / AZURE_CLIENT_ID / AZURE_CLIENT_SECRET), Managed Identity on Azure,
// or the local Azure CLI session (`az login`) during development.
var keyVaultUri = builder.Configuration["KeyVault:Uri"]
    ?? Environment.GetEnvironmentVariable("KEYVAULT_URI");
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    // On Render we authenticate with an explicit Service Principal (env vars). Using
    // ClientSecretCredential directly avoids DefaultAzureCredential probing the whole
    // credential chain (Managed Identity / CLI / MSAL cache), which can SIGSEGV on a
    // minimal Linux container. Locally (no SP env vars) we fall back to `az login`.
    var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
    var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
    var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");

    Azure.Core.TokenCredential credential =
        !string.IsNullOrWhiteSpace(tenantId)
        && !string.IsNullOrWhiteSpace(clientId)
        && !string.IsNullOrWhiteSpace(clientSecret)
            ? new ClientSecretCredential(tenantId, clientId, clientSecret)
            : new DefaultAzureCredential();

    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), credential);
}

const string CorsPolicy = "frontend";

var webRootPath = builder.Environment.WebRootPath
    ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, webRootPath);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Autenticacion JWT.
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Falta 'Jwt:Key'.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "FinancesApi",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "FinancesClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5173",
                "https://finances-frontend-y0sk.onrender.com",
                "https://localhost",
                "http://localhost",
                "capacitor://localhost",
                "ionic://localhost")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Crea la base de datos, aplica migraciones y siembra roles + admin.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    await db.Database.MigrateAsync();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

app.UseExceptionHandler();

// Swagger habilitado en todos los entornos (incluido Production/Render).
app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
