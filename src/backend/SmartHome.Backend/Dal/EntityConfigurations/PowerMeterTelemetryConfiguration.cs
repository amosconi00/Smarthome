using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class PowerMeterTelemetryConfiguration : IEntityTypeConfiguration<PowerMeterTelemetry>
{
    public void Configure(EntityTypeBuilder<PowerMeterTelemetry> builder)
    {
        builder.ToTable("powermeter_telemetries");
        builder.HasKey(e => new { e.Timestamp, e.PowerMeterId });

        builder.Property(e => e.Timestamp)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            .IsRequired();
        builder.Property(e => e.VoltageV);
        builder.Property(e => e.CurrentA);
        builder.Property(e => e.PowerW);
        builder.Property(e => e.EnergyWh);

        builder.HasOne(e => e.PowerMeter)
               .WithMany(pm => pm.Telemetries)
               .HasForeignKey(e => e.PowerMeterId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
