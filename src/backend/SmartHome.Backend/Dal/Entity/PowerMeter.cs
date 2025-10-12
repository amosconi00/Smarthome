namespace SmartHome.Backend.Dal.Entity;

public class PowerMeter
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Level { get; set; }
    // Identification code
    public string Code { get; set; } = string.Empty;

    // Optional: navigation to telemetry entries
    public ICollection<PowerMeterTelemetry> Telemetries { get; set; } = new List<PowerMeterTelemetry>();
}
