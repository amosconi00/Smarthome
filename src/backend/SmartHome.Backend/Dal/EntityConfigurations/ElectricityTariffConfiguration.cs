using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal.EntityConfigurations;

public class ElectricityTariffConfiguration : IEntityTypeConfiguration<ElectricityTariff>
{
    public void Configure(EntityTypeBuilder<ElectricityTariff> builder)
    {
        builder.ToTable("electricity_tariffs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CostF1).IsRequired();
        builder.Property(x => x.CostF2);
        builder.Property(x => x.CostF3);
        builder.Property(x => x.IsTimeOfUse).IsRequired();
        builder.HasIndex(x => x.IsDefault).HasFilter("\"IsDefault\" = true");
    }
}
