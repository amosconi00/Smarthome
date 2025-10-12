using System;

namespace SmartHome.Backend.Dal.Entity;

/// <summary>
/// Keyless entity mapped to SQL view daily_powermeter_consumptions. Contains daily energy (kWh) computed from cumulative EnergyWh.
/// </summary>
public class PowerMeterDailyConsumption
{
    public int PowerMeterId { get; set; }
    public DateOnly Day { get; set; }
    public double EnergyKWh { get; set; }

    public PowerMeter? PowerMeter { get; set; }
}
