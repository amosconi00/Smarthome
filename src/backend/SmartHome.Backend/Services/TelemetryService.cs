using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dal.Entity;

namespace SmartHome.Backend.Services
{
    public interface ITelemetryService
    {
        Task SaveApplianceTelemetryAsync(ApplianceTelemetry telemetry, CancellationToken ct);
        Task SavePowerMeterTelemetryAsync(PowerMeterTelemetry telemetry, CancellationToken ct);
    }

    public class TelemetryService : ITelemetryService
    {
        private readonly IDbContextFactory<SmartHomeDbContext> _dbFactory;
        private readonly ILogger<TelemetryService> _logger;

        public TelemetryService(IDbContextFactory<SmartHomeDbContext> dbFactory, ILogger<TelemetryService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task SaveApplianceTelemetryAsync(ApplianceTelemetry telemetry, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
          
            db.ApplianceTelemetries.Add(telemetry);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving ApplianceTelemetry applianceId={Id}", telemetry?.ApplianceId.ToString() ?? "NO APPLIANCE FOUNDED");
            }
        }

        public async Task SavePowerMeterTelemetryAsync(PowerMeterTelemetry telemetry, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.PowerMeterTelemetries.Add(telemetry);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving PowerMeterTelemetry meterId={Id}", telemetry?.ToString() ?? "NO METER FOUNDED!");
            }
        }
    }
}
