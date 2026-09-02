using System.Reflection;
using Finances.Application.Common.Behaviors;
using Finances.Application.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Finances.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR: registers all command/query handlers in this layer (CQRS).
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // The validation behavior runs before every handler.
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // FluentValidation: registers all validators (AbstractValidator<T>) in this layer.
        services.AddValidatorsFromAssembly(assembly);

        // Legacy service-layer features (not yet migrated to CQRS).
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IIncomeService, IncomeService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IProjectionService, ProjectionService>();
        services.AddScoped<IExchangeService, ExchangeService>();
        return services;
    }
}
