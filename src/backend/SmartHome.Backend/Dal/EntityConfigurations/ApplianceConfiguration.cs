using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class ApplianceConfiguration : IEntityTypeConfiguration<Appliance>
{
    public void Configure(EntityTypeBuilder<Appliance> builder)
    {
        builder.ToTable("appliances");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Brand).HasMaxLength(100);
        builder.Property(e => e.Model).HasMaxLength(100);
        builder.Property(e => e.SerialNumber).HasMaxLength(150);
        builder.Property(e => e.OffThresholdWatts);
        builder.Property(e => e.OnThresholdWatts);
        builder.Property(e => e.SecondsMinDuration);
        builder.Property(e => e.HasStandby);
        builder.Property(e => e.MinStandbyWatts);
        builder.Property(e => e.MaxStandbyWatts);
        builder.Property(e => e.Enable);
        builder.Property(e => e.EnableNilm);

        // Many-to-one: many Appliances can belong to one CategoryAppliance (optional)
        builder.HasOne(a => a.CategoryAppliance)
               .WithMany(c => c.Appliances)
               .HasForeignKey(a => a.CategoryApplianceId)
               .OnDelete(DeleteBehavior.SetNull);

        // One-to-one with Plug (optional from Plug side)
        builder.HasOne(a => a.Plug)
               .WithOne(p => p.Appliance)
               .HasForeignKey<Plug>(p => p.ApplianceId)
               .OnDelete(DeleteBehavior.SetNull);

        // One-to-many with ApplianceSignatures
        builder.HasMany(a => a.Signatures)
               .WithOne(s => s.Appliance)
               .HasForeignKey(s => s.ApplianceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
