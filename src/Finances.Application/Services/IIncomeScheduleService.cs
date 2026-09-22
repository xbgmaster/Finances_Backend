using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IIncomeScheduleService
{
    Task<IReadOnlyList<IncomeScheduleDto>> GetAllAsync(CancellationToken ct = default);
    Task<IncomeScheduleDto> CreateAsync(IncomeScheduleCreateDto dto, CancellationToken ct = default);
    Task<IncomeScheduleDto> UpdateAsync(int id, IncomeScheduleUpdateDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Posts any due pay for the current user's active auto-post schedules. Returns count posted.</summary>
    Task<int> PostDueForCurrentUserAsync(CancellationToken ct = default);

    // Work shifts for hourly jobs.
    Task<IReadOnlyList<WorkShiftDto>> GetShiftsAsync(int year, int month, CancellationToken ct = default);
    Task<WorkShiftDto> CreateShiftAsync(WorkShiftCreateDto dto, CancellationToken ct = default);
    Task<WorkShiftDto> UpdateShiftAsync(int id, WorkShiftUpdateDto dto, CancellationToken ct = default);
    Task DeleteShiftAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// For fixed-salary jobs: stores a pay-occurrence override so the scheduled entry shows the
    /// adjusted amount without immediately posting income (the auto-post uses it on the pay day).
    /// For hourly jobs: creates a direct income (unchanged).
    /// </summary>
    Task CreatePaymentAsync(int jobId, WorkPaymentCreateDto dto, CancellationToken ct = default);

    /// <summary>Pay-occurrence overrides for the current user in a given month.</summary>
    Task<IReadOnlyList<PayOccurrenceOverrideDto>> GetOccurrenceOverridesAsync(int year, int month, CancellationToken ct = default);
}
