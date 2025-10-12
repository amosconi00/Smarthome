using SmartHome.Backend.Dto.Plugs;

namespace SmartHome.Backend.Services
{
    public interface IPlugService
    {
        Task<IEnumerable<PlugDto>> GetAllAsync(CancellationToken cancellationToken);
        Task<PlugDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<PlugDto> CreateAsync(PlugCreateUpdateDto dto, CancellationToken cancellationToken);
        Task<bool> UpdateAsync(int id, PlugCreateUpdateDto dto, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
