using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class IncomeScheduleConfiguration : IEntityTypeConfiguration<IncomeSchedule>
{
    public void Configure(EntityTypeBuilder<IncomeSchedule> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(120);
        builder.Property(s => s.PayType).HasConversion<int>();
        builder.Property(s => s.PayFrequency).HasConversion<int>();
        builder.Property(s => s.Amount).HasPrecision(18, 2);
        builder.Property(s => s.HourlyRate).HasPrecision(18, 2);
        builder.Property(s => s.Currency).IsRequired().HasMaxLength(3);
        builder.Property(s => s.Color).HasMaxLength(9);
        builder.Property(s => s.LastPostedPeriod).HasMaxLength(8);

        builder.Property(s => s.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(s => s.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional account; deleting it keeps the schedule (link becomes null).
        builder.HasOne(s => s.PaymentMethod)
            .WithMany()
            .HasForeignKey(s => s.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(s => s.PaymentMethodId);
    }
}
