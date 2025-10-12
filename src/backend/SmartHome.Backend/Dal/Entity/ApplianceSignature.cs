namespace SmartHome.Backend.Dal.Entity;

public sealed class ApplianceSignature : SignatureBase
{
    public int Id { get; set; }

    public Appliance Appliance { get; set; } = null!;
    public int ApplianceId { get; set; } // FK

    public string Name { get; set; } = string.Empty; // reintroduced for clarity/EF config

    // Versione incrementale per singolo appliance (1,2,3,...)
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }

    // Solo una signature per appliance può essere default (attiva)
    public bool IsDefault { get; set; }
}