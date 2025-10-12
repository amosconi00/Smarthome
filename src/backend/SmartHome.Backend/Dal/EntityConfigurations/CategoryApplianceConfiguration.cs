using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class CategoryApplianceConfiguration : IEntityTypeConfiguration<CategoryAppliance>
{
    public void Configure(EntityTypeBuilder<CategoryAppliance> builder)
    {
        builder.ToTable("appliance_categories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(400);

        // The relationship is configured from ApplianceConfiguration.
    }
}
