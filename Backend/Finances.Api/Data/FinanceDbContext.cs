using Finances.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Finances.Api.Data;

public class FinanceDbContext : DbContext
{
    public FinanceDbContext(DbContextOptions<FinanceDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.MonthlyBudget).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Income>(e =>
        {
            e.Property(i => i.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasOne(x => x.Category)
                .WithMany(c => c.Expenses)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Categorias iniciales para que la app sea util desde el primer arranque.
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Comida", Icon = "utensils", Color = "#f59e0b", MonthlyBudget = 400 },
            new Category { Id = 2, Name = "Transporte", Icon = "car", Color = "#3b82f6", MonthlyBudget = 150 },
            new Category { Id = 3, Name = "Vivienda", Icon = "home", Color = "#10b981", MonthlyBudget = 800 },
            new Category { Id = 4, Name = "Entretenimiento", Icon = "film", Color = "#ec4899", MonthlyBudget = 120 },
            new Category { Id = 5, Name = "Salud", Icon = "heart", Color = "#ef4444", MonthlyBudget = 100 },
            new Category { Id = 6, Name = "Compras", Icon = "shopping-bag", Color = "#8b5cf6", MonthlyBudget = 200 },
            new Category { Id = 7, Name = "Servicios", Icon = "bolt", Color = "#14b8a6", MonthlyBudget = 180 },
            new Category { Id = 8, Name = "Otros", Icon = "tag", Color = "#64748b", MonthlyBudget = null }
        );
    }
}
