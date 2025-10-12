using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class CategoryApplianceSignatureConfiguration : IEntityTypeConfiguration<CategoryApplianceSignature>
{
    public void Configure(EntityTypeBuilder<CategoryApplianceSignature> builder)
    {
        builder.ToTable("category_appliance_signatures");
        builder.HasKey(s => s.CategoryApplianceId); // 1:1 PK = FK pattern

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.OnDeltaMinW);
        builder.Property(s => s.OnDeltaMaxW);
        builder.Property(s => s.OffDeltaMinW);
        builder.Property(s => s.OffDeltaMaxW);
        builder.Property(s => s.MinOnSeconds);
        builder.Property(s => s.RefractorySeconds);

        builder.HasOne(s => s.CategoryAppliance)
               .WithOne(c => c.Signature)
               .HasForeignKey<CategoryApplianceSignature>(s => s.CategoryApplianceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
