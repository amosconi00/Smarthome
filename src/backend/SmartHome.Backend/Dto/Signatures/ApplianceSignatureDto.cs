namespace SmartHome.Backend.Dto.Signatures;

public class ApplianceSignatureDto
{
    public int Id { get; set; }
    public int ApplianceId { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDefault { get; set; }
    public string Name { get; set; } = string.Empty;
    public double OnDeltaMinW { get; set; }
    public double OnDeltaMaxW { get; set; }
    public double OffDeltaMinW { get; set; }
    public double OffDeltaMaxW { get; set; }
    public double MinOnSeconds { get; set; }
    public double RefractorySeconds { get; set; }
}

