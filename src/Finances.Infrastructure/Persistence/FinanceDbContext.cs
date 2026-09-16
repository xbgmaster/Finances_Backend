using System.Reflection;
using Finances.Application.Common;
using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Finances.Infrastructure.Persistence;

public class FinanceDbContext : IdentityDbContext<ApplicationUser>, IFinanceDbContext
{
    public FinanceDbContext(DbContextOptions<FinanceDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<IncomeSchedule> IncomeSchedules => Set<IncomeSchedule>();
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Credit> Credits => Set<Credit>();
    public DbSet<CreditPayment> CreditPayments => Set<CreditPayment>();
    public DbSet<CurrencyExchange> CurrencyExchanges => Set<CurrencyExchange>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<CardPayment> CardPayments => Set<CardPayment>();
    public DbSet<Identity.RefreshToken> RefreshTokens => Set<Identity.RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
