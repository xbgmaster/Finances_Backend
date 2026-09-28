using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class ExpenseScheduleConfiguration : IEntityTypeConfiguration<ExpenseSchedule>
{
    public void Configure(EntityTypeBuilder<ExpenseSchedule> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(120);
        builder.Property(s => s.PayFrequency).HasConversion<int>();
        builder.Property(s => s.Amount).HasPrecision(18, 2);
        builder.Property(s => s.Currency).IsRequired().HasMaxLength(3);
        builder.Property(s => s.LastPostedPeriod).HasMaxLength(8);

        builder.HasOne(s => s.Category)
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.CategoryId);

        builder.HasOne(s => s.PaymentMethod)
            .WithMany()
            .HasForeignKey(s => s.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.PaymentMethodId);

        builder.Property(s => s.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(s => s.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
