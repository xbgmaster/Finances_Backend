using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.Property(p => p.Name).IsRequired().HasMaxLength(80);
        builder.Property(p => p.Currency).HasMaxLength(3);
        builder.Property(p => p.Color).HasMaxLength(9);
        builder.Property(p => p.Icon).HasMaxLength(60);
        builder.Property(p => p.CreditLimit).HasPrecision(18, 2);
        builder.Property(p => p.LastReminderKey).HasMaxLength(40);

        builder.Property(p => p.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(p => p.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
