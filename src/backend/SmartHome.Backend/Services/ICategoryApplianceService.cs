using SmartHome.Backend.Dto.CategoryAppliances;

namespace SmartHome.Backend.Services
{
    public interface ICategoryApplianceService
    {
        Task<IEnumerable<CategoryApplianceDto>> GetAllAsync(CancellationToken cancellationToken);
        Task<CategoryApplianceDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<CategoryApplianceDto> CreateAsync(CategoryApplianceCreateUpdateDto dto, CancellationToken cancellationToken);
        Task<bool> UpdateAsync(int id, CategoryApplianceCreateUpdateDto dto, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
