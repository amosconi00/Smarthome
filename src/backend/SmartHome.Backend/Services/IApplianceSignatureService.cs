using SmartHome.Backend.Dto.Signatures;
using SmartHome.Backend.Dto.Appliances;

namespace SmartHome.Backend.Services;

public interface IApplianceSignatureService
{
    Task<IEnumerable<ApplianceSignatureDto>> GetAllAsync(CancellationToken ct);
    Task<IEnumerable<ApplianceSignatureDto>> GetByApplianceAsync(int applianceId, CancellationToken ct);
    Task<ApplianceSignatureDto?> GetDefaultAsync(int applianceId, CancellationToken ct);
    Task<ApplianceSignatureDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<ApplianceSignatureDto?> CreateAsync(int applianceId, ApplianceSignatureCreateUpdateDto dto, CancellationToken ct);
    Task<bool> UpdateAsync(int id, ApplianceSignatureCreateUpdateDto dto, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
