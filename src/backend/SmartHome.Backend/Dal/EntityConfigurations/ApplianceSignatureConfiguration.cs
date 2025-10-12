using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class ApplianceSignatureConfiguration : IEntityTypeConfiguration<ApplianceSignature>
{
    public void Configure(EntityTypeBuilder<ApplianceSignature> builder)
    {
        builder.ToTable("appliance_signatures");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.OnDeltaMinW);
        builder.Property(s => s.OnDeltaMaxW);
        builder.Property(s => s.OffDeltaMinW);
        builder.Property(s => s.OffDeltaMaxW);
        builder.Property(s => s.MinOnSeconds);
        builder.Property(s => s.RefractorySeconds);
        builder.Property(s => s.Version).IsRequired();
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(s => s.IsDefault).HasDefaultValue(false);

        builder.HasIndex(s => new { s.ApplianceId, s.Version }).IsUnique();
        // PostgreSQL filter syntax (remove SQL Server square brackets)
        builder.HasIndex(s => s.ApplianceId)
               .HasFilter("\"IsDefault\" = true")
               .IsUnique();

        builder.HasOne(s => s.Appliance)
               .WithMany(a => a.Signatures)
               .HasForeignKey(s => s.ApplianceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
