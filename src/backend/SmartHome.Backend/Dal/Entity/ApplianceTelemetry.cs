using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace SmartHome.Backend.Dal.Entity;

public class ApplianceTelemetry : BaseTelemetry
{
    public double? PowerWatts { get; set; }

    public int ApplianceId { get; set; }
    public Appliance? Appliance { get; set; }
}