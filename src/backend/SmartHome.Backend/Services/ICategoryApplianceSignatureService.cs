using SmartHome.Backend.Dto.CategoryAppliances;
using SmartHome.Backend.Dto.Signatures;

namespace SmartHome.Backend.Services;

public interface ICategoryApplianceSignatureService
{
    Task<IEnumerable<CategoryApplianceSignatureDto>> GetAllAsync(CancellationToken ct);
    Task<CategoryApplianceSignatureDto?> GetByIdAsync(int categoryApplianceId, CancellationToken ct);
    Task<CategoryApplianceSignatureDto?> CreateAsync(int categoryApplianceId, CategoryApplianceSignatureCreateUpdateDto dto, CancellationToken ct);
    Task<bool> UpdateAsync(int categoryApplianceId, CategoryApplianceSignatureCreateUpdateDto dto, CancellationToken ct);
    Task<bool> DeleteAsync(int categoryApplianceId, CancellationToken ct);
}
