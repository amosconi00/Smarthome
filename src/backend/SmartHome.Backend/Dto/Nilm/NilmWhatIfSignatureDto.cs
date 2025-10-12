namespace SmartHome.Backend.Dto.Nilm;

// DTO per what-if: parametri signature Hart senza persistenza
public class NilmWhatIfSignatureDto
{
    public double OnDeltaMinW { get; set; }
    public double OnDeltaMaxW { get; set; }
    public double OffDeltaMinW { get; set; }
    public double OffDeltaMaxW { get; set; }
    public double MinOnSeconds { get; set; } = 10;
    public double RefractorySeconds { get; set; } = 5;
}
