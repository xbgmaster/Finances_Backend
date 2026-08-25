using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class CreditPaymentConfiguration : IEntityTypeConfiguration<CreditPayment>
{
    public void Configure(EntityTypeBuilder<CreditPayment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(30).HasDefaultValue(CreditPaymentType.Installment);
        builder.Property(p => p.Effect).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Note).HasMaxLength(300);

        builder.Property(p => p.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.CreditId);

        // The Credit -> Payments relationship (with cascade) is configured in
        // CreditConfiguration; UserId is kept only as an indexed scoping column
        // (no separate FK) to avoid multiple cascade paths to the user.
    }
}
