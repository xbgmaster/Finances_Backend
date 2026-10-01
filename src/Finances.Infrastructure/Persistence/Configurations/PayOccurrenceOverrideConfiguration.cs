using Finances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class PayOccurrenceOverrideConfiguration : IEntityTypeConfiguration<PayOccurrenceOverride>
{
    public void Configure(EntityTypeBuilder<PayOccurrenceOverride> builder)
    {
        builder.Property(o => o.Amount).HasPrecision(18, 2);
        builder.Property(o => o.Description).HasMaxLength(200);
        builder.Property(o => o.UserId).IsRequired().HasMaxLength(450);

        // One override per job per date (upsert semantics).
        builder.HasIndex(o => new { o.IncomeScheduleId, o.PayDate }).IsUnique();

        builder.HasOne(o => o.IncomeSchedule)
            .WithMany()
            .HasForeignKey(o => o.IncomeScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
