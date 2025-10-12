using System;

namespace SmartHome.Backend.Dal.Entity;

public class PowerMeterTelemetry : BaseTelemetry
{
    // FK to PowerMeter
    public int PowerMeterId { get; set; }
    public PowerMeter? PowerMeter { get; set; }

    // Measurements
    public double? VoltageV { get; set; }
    public double? CurrentA { get; set; }
    public double? PowerW { get; set; }
    public double? EnergyWh { get; set; }
}
