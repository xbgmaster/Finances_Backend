using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.Property(e => e.ReceiptUrl).HasMaxLength(300);
        builder.Property(e => e.Currency).HasMaxLength(3);

        builder.HasOne(e => e.Category)
            .WithMany(c => c.Expenses)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // An expense may be linked one-to-one to the credit payment that generated it.
        // Deleting the payment (or its parent credit, which cascades to payments) removes
        // the mirrored expense automatically.
        builder.HasOne(e => e.CreditPayment)
            .WithOne()
            .HasForeignKey<Expense>(e => e.CreditPaymentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.CreditPaymentId).IsUnique();

        // A payment method is optional; deleting it keeps the expense (method becomes null).
        builder.HasOne(e => e.PaymentMethod)
            .WithMany()
            .HasForeignKey(e => e.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => e.PaymentMethodId);

        builder.Property(e => e.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(e => e.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
