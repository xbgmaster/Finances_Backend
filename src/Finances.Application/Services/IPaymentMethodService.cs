using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IPaymentMethodService
{
    Task<IReadOnlyList<PaymentMethodDto>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<PaymentMethodDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PaymentMethodDto> CreateAsync(PaymentMethodCreateDto dto, CancellationToken ct = default);
    Task<PaymentMethodDto> UpdateAsync(int id, PaymentMethodCreateDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
