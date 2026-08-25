using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class CurrencyExchangeConfiguration : IEntityTypeConfiguration<CurrencyExchange>
{
    public void Configure(EntityTypeBuilder<CurrencyExchange> builder)
    {
        builder.Property(x => x.FromCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.ToCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.FromAmount).HasPrecision(18, 2);
        builder.Property(x => x.ToAmount).HasPrecision(18, 2);
        builder.Property(x => x.Rate).HasPrecision(18, 6);
        builder.Property(x => x.Note).HasMaxLength(300);

        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(x => x.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
