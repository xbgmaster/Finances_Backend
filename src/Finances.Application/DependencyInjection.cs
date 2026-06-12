using Finances.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Finances.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IIncomeService, IncomeService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IProjectionService, ProjectionService>();
        return services;
    }
}
