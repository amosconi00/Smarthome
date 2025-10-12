namespace SmartHome.Backend.Dal.Entity;

public sealed class CategoryApplianceSignature : SignatureBase
{
    public int CategoryApplianceId { get; init; }

    public string Name { get; set; } = string.Empty; // added

    // Navigation
    public CategoryAppliance CategoryAppliance { get; init; } = null!;
}
