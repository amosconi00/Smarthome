namespace SmartHome.Backend.Dal.Entity;

public class ElectricityTariff
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // ex: "Residential 2025"
    public bool IsTimeOfUse { get; set; } // true if F1/F2/F3 time-of-use, false if single rate (use CostF1 only)

    // Backward compatibility alias (old name). Avoid persistence mapping differences.
    public bool IsMultiFascia
    {
        get => IsTimeOfUse;
        set => IsTimeOfUse = value;
    }

    // Costs (Euro per kWh)
    public double CostF1 { get; set; }
    public double? CostF2 { get; set; }
    public double? CostF3 { get; set; }

    // Flag default
    public bool IsDefault { get; set; }

    // Periodo validità opzionale
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }
}
