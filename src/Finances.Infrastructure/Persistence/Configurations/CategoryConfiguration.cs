using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
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

        builder.Property(c => c.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(c => c.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
