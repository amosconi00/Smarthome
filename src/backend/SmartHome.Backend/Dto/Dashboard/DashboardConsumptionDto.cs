namespace SmartHome.Backend.Dto.Dashboard;

public record DashboardConsumptionDto(
    DateTime DayStartUtc,
    DateTime MonthStartUtc,
    DateTime NowUtc,
    double DayKWh,
    double MonthKWh,
    double? DayCostEuro,
    double? MonthCostEuro,
    string? TariffName
);
