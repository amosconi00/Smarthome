using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class ApplianceTelemetryConfiguration : IEntityTypeConfiguration<ApplianceTelemetry>
{
    public void Configure(EntityTypeBuilder<ApplianceTelemetry> builder)
    {
        builder.ToTable("appliance_telemetries");
        builder.HasKey(e => new { e.Timestamp, e.ApplianceId });

        builder.Property(e => e.Timestamp)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            .IsRequired();
        builder.Property(e => e.PowerWatts);

        builder.HasOne(e => e.Appliance)
               .WithMany(a => a.Telemetries)
               .HasForeignKey(e => e.ApplianceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
