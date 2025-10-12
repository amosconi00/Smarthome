using SmartHome.Backend.Dal.Entity;
namespace SmartHome.Backend.Dal.Entity;

/// <summary>
/// Rilevamento NILM (stima stato/potenza di un elettrodomestico) prodotto da diversi algoritmi (CO, Seq2Point, ecc.).
/// </summary>
public class NilmDetection: BaseTelemetry
{
    public long Number { get; set; } 
    public int? ApplianceId { get; set; }
    public Appliance? Appliance { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public double? EstimatedPowerW { get; set; }
    public bool? IsOn { get; set; }
}
