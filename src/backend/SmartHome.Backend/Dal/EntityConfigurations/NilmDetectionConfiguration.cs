using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class NilmDetectionConfiguration : IEntityTypeConfiguration<NilmDetection>
{
    public void Configure(EntityTypeBuilder<NilmDetection> builder)
    {
        builder.ToTable("nilm_detections");
        builder.HasKey(x => new { x.Timestamp , x.Number});
        builder.Property(x => x.Number).ValueGeneratedOnAdd();
        builder.Property(x => x.Timestamp)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            .IsRequired();
        builder.Property(x => x.Source).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ModelName).HasMaxLength(100);
        builder.HasIndex(x => new { x.Timestamp, x.Source, x.ModelName });
        builder.HasIndex(x => new { x.Timestamp, x.ApplianceId });
        builder.HasOne(x => x.Appliance)
            .WithMany()
            .HasForeignKey(x => x.ApplianceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
