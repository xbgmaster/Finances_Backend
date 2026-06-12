using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Finances.Infrastructure.Persistence;

/// <summary>
/// Fabrica usada por las herramientas de EF (dotnet ef) para crear el contexto
/// en tiempo de diseno. La cadena real se aplica en runtime via Database.Migrate().
/// </summary>
public class FinanceDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("FINANCES_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=finances;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new FinanceDbContext(options);
    }
}
