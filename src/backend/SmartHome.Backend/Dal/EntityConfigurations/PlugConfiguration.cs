using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class PlugConfiguration : IEntityTypeConfiguration<Plug>
{
    public void Configure(EntityTypeBuilder<Plug> builder)
    {
        builder.ToTable("plugs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Manufacturer).HasMaxLength(100);
        builder.Property(e => e.Model).HasMaxLength(100);
        builder.Property(e => e.MacAddress).HasMaxLength(50);
        builder.Property(e => e.IpAddress).HasMaxLength(45);
        builder.Property(e => e.FirmwareVersion).HasMaxLength(50);
        builder.Property(e => e.IsOnline).HasDefaultValue(false);

        // Optional FK to Appliance
        builder.HasOne(p => p.Appliance)
               .WithOne(a => a.Plug)
               .HasForeignKey<Plug>(p => p.ApplianceId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
