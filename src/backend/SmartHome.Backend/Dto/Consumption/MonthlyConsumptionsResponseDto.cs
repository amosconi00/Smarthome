using System.Collections.Generic;

namespace SmartHome.Backend.Dto.Consumption;

public record MonthlyConsumptionsResponseDto(int Year, int Month, int EntityId, IReadOnlyCollection<DailyConsumptionDto> Days, double TotalKWh);
