namespace SmartHome.Backend.Dal.Entity;

public class Appliance
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public double OffThresholdWatts { get; set; }
    public double OnThresholdWatts { get; set; }
    public double SecondsMinDuration { get; set; }
    public bool HasStandby { get; set; }
    public double MinStandbyWatts { get; set; }
    public double MaxStandbyWatts { get; set; }
    // Indicates if this appliance is enabled globally
    public bool Enable { get; set; }
    // Indicates if this appliance is enabled for NILM processing
    public bool EnableNilm { get; set; }

    // Foreign key to CategoryAppliance (one-to-one, optional)
    public int? CategoryApplianceId { get; set; }
    public CategoryAppliance? CategoryAppliance { get; set; }

    // One-to-one navigation to Plug (optional)
    public Plug? Plug { get; set; }

    // Signatures (one-to-many)
    public ICollection<ApplianceSignature> Signatures { get; set; } = new List<ApplianceSignature>();

    // Optional: navigation to telemetry entries
    public ICollection<ApplianceTelemetry> Telemetries { get; set; } = new List<ApplianceTelemetry>();
}
