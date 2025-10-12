using SmartHome.Backend.Dto.Tariffs;

namespace SmartHome.Backend.Services;

public interface IElectricityTariffService
{
    Task<IReadOnlyList<ElectricityTariffDto>> GetAllAsync(CancellationToken ct);
    Task<ElectricityTariffDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<ElectricityTariffDto> CreateAsync(ElectricityTariffCreateUpdateDto dto, CancellationToken ct);
    Task<bool> UpdateAsync(int id, ElectricityTariffCreateUpdateDto dto, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
