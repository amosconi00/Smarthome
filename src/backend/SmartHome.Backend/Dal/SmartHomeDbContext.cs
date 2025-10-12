using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Dal
{
    public class SmartHomeDbContext : DbContext
    {
        public SmartHomeDbContext(DbContextOptions<SmartHomeDbContext> options) : base(options)
        {
        }

        public DbSet<Appliance> Appliances => Set<Appliance>();
        public DbSet<Plug> Plugs => Set<Plug>();
        public DbSet<CategoryAppliance> CategoryAppliances => Set<CategoryAppliance>();
        public DbSet<PowerMeter> PowerMeters => Set<PowerMeter>();
        public DbSet<ApplianceTelemetry> ApplianceTelemetries => Set<ApplianceTelemetry>();
        public DbSet<PowerMeterTelemetry> PowerMeterTelemetries => Set<PowerMeterTelemetry>();
        public DbSet<ApplianceSignature> ApplianceSignatures => Set<ApplianceSignature>();
        public DbSet<CategoryApplianceSignature> CategoryApplianceSignatures => Set<CategoryApplianceSignature>();
        public DbSet<NilmDetection> NilmDetections => Set<NilmDetection>();
        public DbSet<ElectricityTariff> ElectricityTariffs => Set<ElectricityTariff>();
        public DbSet<PowerMeterDailyConsumption> PowerMeterDailyConsumptions => Set<PowerMeterDailyConsumption>();
        public DbSet<ApplianceDailyConsumption> ApplianceDailyConsumptions => Set<ApplianceDailyConsumption>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Apply all IEntityTypeConfiguration implementations in this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartHomeDbContext).Assembly);

            // Keyless views configuration
            modelBuilder.Entity<PowerMeterDailyConsumption>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("daily_powermeter_consumptions");
                eb.Property(p => p.Day).HasColumnName("day");
                eb.Property(p => p.PowerMeterId).HasColumnName("powermeter_id");
                eb.Property(p => p.EnergyKWh).HasColumnName("energy_kwh");
                eb.HasOne(p => p.PowerMeter).WithMany().HasForeignKey(p => p.PowerMeterId);
            });
            modelBuilder.Entity<ApplianceDailyConsumption>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("daily_appliance_consumptions");
                eb.Property(p => p.Day).HasColumnName("day");
                eb.Property(p => p.ApplianceId).HasColumnName("appliance_id");
                eb.Property(p => p.EnergyKWh).HasColumnName("energy_kwh");
                eb.HasOne(p => p.Appliance).WithMany().HasForeignKey(p => p.ApplianceId);
            });
        }
    }
}
