namespace SmartHome.Backend.Dal.Entity;

public class Plug
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? MacAddress { get; set; }
    public string? IpAddress { get; set; }
    public string? FirmwareVersion { get; set; }
    public bool IsOnline { get; set; }

    // Foreign key and one-to-one back reference to Appliance (optional)
    public int? ApplianceId { get; set; }
    public Appliance? Appliance { get; set; }
}
