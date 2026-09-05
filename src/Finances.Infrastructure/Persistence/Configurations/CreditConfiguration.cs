using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class CreditConfiguration : IEntityTypeConfiguration<Credit>
{
    public void Configure(EntityTypeBuilder<Credit> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(120);
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.InterestModel).HasConversion<string>().HasMaxLength(30).HasDefaultValue(InterestModel.CompoundFrench);
        builder.Property(c => c.RateConvention).HasConversion<string>().HasMaxLength(20).HasDefaultValue(RateConvention.Nominal);
        builder.Property(c => c.PrepaymentRebateMethod).HasConversion<string>().HasMaxLength(30).HasDefaultValue(PrepaymentRebateMethod.None);
        builder.Property(c => c.PrepaymentEffect).HasConversion<string>().HasMaxLength(30).HasDefaultValue(PrepaymentEffect.ReduceTerm);
        builder.Property(c => c.PrepaymentPenaltyRate).HasPrecision(9, 4);
        builder.Property(c => c.MonthlyInsuranceRate).HasPrecision(9, 6).HasDefaultValue(0m);
        builder.Property(c => c.MonthlyFixedFee).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(c => c.PaymentDueDay).HasDefaultValue(1);
        builder.Property(c => c.LastReminderKey).HasMaxLength(40);
        builder.Property(c => c.Principal).HasPrecision(18, 2);
        builder.Property(c => c.AnnualInterestRate).HasPrecision(9, 4);
        builder.Property(c => c.Currency).HasMaxLength(3);

        builder.Property(c => c.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(c => c.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Payments)
            .WithOne(p => p.Credit)
            .HasForeignKey(p => p.CreditId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
