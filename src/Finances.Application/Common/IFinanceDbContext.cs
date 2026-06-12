using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finances.Application.Common;

/// <summary>
/// Abstraccion del contexto de datos para que la capa Application no dependa
/// del proveedor concreto (PostgreSQL/EF). La implementa Infrastructure.
/// </summary>
public interface IFinanceDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Income> Incomes { get; }
    DbSet<Expense> Expenses { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
