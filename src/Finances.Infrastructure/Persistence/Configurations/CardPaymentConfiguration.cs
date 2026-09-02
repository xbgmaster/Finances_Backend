using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class CardPaymentConfiguration : IEntityTypeConfiguration<CardPayment>
{
    public void Configure(EntityTypeBuilder<CardPayment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Note).HasMaxLength(300);

        builder.Property(p => p.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.CreditCardId);
        builder.HasIndex(p => p.SourcePaymentMethodId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting the card removes its payment history too.
        builder.HasOne(p => p.CreditCard)
            .WithMany()
            .HasForeignKey(p => p.CreditCardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting the funding account keeps the payment (it just becomes external).
        builder.HasOne(p => p.SourcePaymentMethod)
            .WithMany()
            .HasForeignKey(p => p.SourcePaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
