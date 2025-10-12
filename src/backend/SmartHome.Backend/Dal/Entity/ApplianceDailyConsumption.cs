using System;

namespace SmartHome.Backend.Dal.Entity;

/// <summary>
/// Keyless entity mapped to SQL view daily_appliance_consumptions. Energy kWh derived by integrating power samples.
/// </summary>
public class ApplianceDailyConsumption
{
    public int ApplianceId { get; set; }
    public DateOnly Day { get; set; }
    public double EnergyKWh { get; set; }

    public Appliance? Appliance { get; set; }
}
