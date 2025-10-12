namespace SmartHome.Backend.Dto.Tariffs;

public record ElectricityTariffDto(int Id, string Name, bool IsTimeOfUse, double CostF1, double? CostF2, double? CostF3, bool IsDefault, DateTime? ValidFromUtc, DateTime? ValidToUtc);
public record ElectricityTariffCreateUpdateDto(string Name, bool IsTimeOfUse, double CostF1, double? CostF2, double? CostF3, bool IsDefault, DateTime? ValidFromUtc, DateTime? ValidToUtc);
