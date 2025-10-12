namespace SmartHome.Backend.Dal.Entity;

/// <summary>
/// Base astratta per le signature (appliance e categoria) con stessa struttura di parametri.
/// Non viene esposta nel DbContext come DbSet per mantenere tabelle separate per i derivati.
/// </summary>
public abstract class SignatureBase
{
    // Range dei salti tipici (W)
    public double OnDeltaMinW { get; set; }
    public double OnDeltaMaxW { get; set; }
    public double OffDeltaMinW { get; set; }   // confronto su |?|
    public double OffDeltaMaxW { get; set; }

    // Vincoli temporali
    public double MinOnSeconds { get; set; } = 10;
    public double RefractorySeconds { get; set; } = 5;
}
