using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface IExchangeService
{
    Task<IReadOnlyList<ExchangeDto>> GetAllAsync(CancellationToken ct = default);
    Task<ExchangeDto> CreateAsync(ExchangeCreateDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
