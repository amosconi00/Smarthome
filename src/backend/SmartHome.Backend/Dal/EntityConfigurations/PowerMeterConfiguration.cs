using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class PowerMeterConfiguration : IEntityTypeConfiguration<PowerMeter>
{
    public void Configure(EntityTypeBuilder<PowerMeter> builder)
    {
        builder.ToTable("powermeters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(400);
        builder.Property(e => e.Level).IsRequired();
        builder.Property(e => e.Code).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Code).IsUnique();
    }
}
