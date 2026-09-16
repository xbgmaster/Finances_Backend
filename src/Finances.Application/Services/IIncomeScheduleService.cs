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

    /// <summary>Posts a direct one-off payment (income) attributed to a job on a given day.</summary>
    Task CreatePaymentAsync(int jobId, WorkPaymentCreateDto dto, CancellationToken ct = default);
}
