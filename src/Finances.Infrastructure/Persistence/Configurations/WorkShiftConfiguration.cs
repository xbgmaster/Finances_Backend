using Finances.Domain.Entities;
using Finances.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finances.Infrastructure.Persistence.Configurations;

public class WorkShiftConfiguration : IEntityTypeConfiguration<WorkShift>
{
    public void Configure(EntityTypeBuilder<WorkShift> builder)
    {
        builder.Property(s => s.Hours).HasPrecision(9, 2);
        builder.Property(s => s.HourlyRate).HasPrecision(18, 2);
        builder.Property(s => s.Amount).HasPrecision(18, 2);
        builder.Property(s => s.Currency).IsRequired().HasMaxLength(3);

        builder.Property(s => s.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(s => s.UserId);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a job removes its shifts.
        builder.HasOne(s => s.IncomeSchedule)
            .WithMany()
            .HasForeignKey(s => s.IncomeScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.IncomeScheduleId);

        // Deleting the posted income just unlinks the shift so it can be swept into the next cut.
        builder.HasOne(s => s.Income)
            .WithMany()
            .HasForeignKey(s => s.IncomeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(s => s.IncomeId);
    }
}
