using SmartHome.Backend.Dto.Appliances;

namespace SmartHome.Backend.Services
{
    public interface IApplianceService
    {
        Task<IEnumerable<ApplianceDto>> GetAllAsync(CancellationToken cancellationToken);
        Task<ApplianceDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<ApplianceDto> CreateAsync(ApplianceCreateUpdateDto dto, CancellationToken cancellationToken);
        Task<bool> UpdateAsync(int id, ApplianceCreateUpdateDto dto, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
