using Finances.Application.Dtos;

namespace Finances.Application.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CategoryCreateDto dto, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(int id, CategoryCreateDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
