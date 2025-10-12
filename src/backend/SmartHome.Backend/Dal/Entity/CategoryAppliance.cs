namespace SmartHome.Backend.Dal.Entity;

public class CategoryAppliance
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
   
    // One-to-many back-reference to Appliances
    public ICollection<Appliance> Appliances { get; set; } = new List<Appliance>();

    // One-to-one navigation to CategoryApplianceSignature
    public CategoryApplianceSignature? Signature { get; set; }
}
