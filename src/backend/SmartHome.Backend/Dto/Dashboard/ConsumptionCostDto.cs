namespace SmartHome.Backend.Dto.Dashboard;

public record ConsumptionCostDto(
    int? ApplianceId,
    string? ApplianceName,
    string Source,          // "APPLIANCE", "CO", "S2P"
    string? ModelName,      // per S2P (categoria / modello) opzionale
    double EnergyKWh,
    double? CostEuro
);
