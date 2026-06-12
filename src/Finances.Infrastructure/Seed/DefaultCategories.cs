using Finances.Domain.Entities;

namespace Finances.Infrastructure.Seed;

/// <summary>Categorias por defecto que se crean para cada usuario nuevo.</summary>
public static class DefaultCategories
{
    private static readonly (string Name, string Icon, string Color, decimal? Budget)[] Items =
    {
        ("Comida", "utensils", "#f59e0b", 400m),
        ("Transporte", "car", "#3b82f6", 150m),
        ("Vivienda", "home", "#10b981", 800m),
        ("Entretenimiento", "film", "#ec4899", 120m),
        ("Salud", "heart", "#ef4444", 100m),
        ("Compras", "shopping-bag", "#8b5cf6", 200m),
        ("Servicios", "bolt", "#14b8a6", 180m),
        ("Otros", "tag", "#64748b", null),
    };

    public static IEnumerable<Category> For(string userId) =>
        Items.Select(i => new Category
        {
            Name = i.Name,
            Icon = i.Icon,
            Color = i.Color,
            MonthlyBudget = i.Budget,
            UserId = userId
        });
}
