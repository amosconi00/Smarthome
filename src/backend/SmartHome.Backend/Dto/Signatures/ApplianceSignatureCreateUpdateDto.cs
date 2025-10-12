using System.ComponentModel.DataAnnotations;

namespace SmartHome.Backend.Dto.Signatures;

public class ApplianceSignatureCreateUpdateDto
{
    [Required] public string Name { get; set; } = string.Empty;
    [Range(0,double.MaxValue)] public double OnDeltaMinW { get; set; }
    [Range(0,double.MaxValue)] public double OnDeltaMaxW { get; set; }
    [Range(0,double.MaxValue)] public double OffDeltaMinW { get; set; }
    [Range(0,double.MaxValue)] public double OffDeltaMaxW { get; set; }
    [Range(0,double.MaxValue)] public double MinOnSeconds { get; set; } = 10;
    [Range(0,double.MaxValue)] public double RefractorySeconds { get; set; } = 5;
    public bool IsDefault { get; set; }
}
