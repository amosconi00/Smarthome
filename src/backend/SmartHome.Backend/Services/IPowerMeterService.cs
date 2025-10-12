using SmartHome.Backend.Dto.PowerMeters;

namespace SmartHome.Backend.Services
{
    public interface IPowerMeterService
    {
        Task<IEnumerable<PowerMeterDto>> GetAllAsync(CancellationToken cancellationToken);
        Task<PowerMeterDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<PowerMeterDto> CreateAsync(PowerMeterCreateUpdateDto dto, CancellationToken cancellationToken);
        Task<bool> UpdateAsync(int id, PowerMeterCreateUpdateDto dto, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
