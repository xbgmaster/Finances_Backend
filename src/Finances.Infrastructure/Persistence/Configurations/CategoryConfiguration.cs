using System.Text.Json;
using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(80);
        builder.Property(c => c.Icon).HasMaxLength(60);
        builder.Property(c => c.Color).HasMaxLength(9);
        builder.Property(c => c.MonthlyBudget).HasPrecision(18, 2);
        builder.Property(c => c.IsSystem).HasDefaultValue(false);

        // Per-currency budgets stored as a jsonb dictionary { "USD": 500, "COP": 1500000 }.
        var jsonOptions = new JsonSerializerOptions();
        builder.Property(c => c.Budgets)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v ?? new Dictionary<string, decimal>(), jsonOptions),
                v => string.IsNullOrWhiteSpace(v)
                    ? new Dictionary<string, decimal>()
                    : JsonSerializer.Deserialize<Dictionary<string, decimal>>(v, jsonOptions) ?? new Dictionary<string, decimal>())
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, decimal>>(
                (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                v => v == null ? 0 : JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                v => v == null ? new Dictionary<string, decimal>() : new Dictionary<string, decimal>(v)));

        builder.Property(c => c.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(c => c.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
