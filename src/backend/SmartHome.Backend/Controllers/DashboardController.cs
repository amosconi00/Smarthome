using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dto.Dashboard;
using SmartHome.Backend.Extensions;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDbContextFactory<SmartHomeDbContext> _ctxFactory;
    public DashboardController(IDbContextFactory<SmartHomeDbContext> ctxFactory)
    {
        _ctxFactory = ctxFactory;
    }

    /// <summary>
    /// Consumi aggregati (kWh) del giorno corrente e del mese corrente calcolati dai dati del power meter + costo usando tariffa di default.
    /// </summary>
    [HttpGet("consumption")]
    [ProducesResponseType(typeof(DashboardConsumptionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardConsumptionDto>> GetConsumption(CancellationToken ct)
    {
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        async Task<double> ComputeEnergyKWh(DateTime from, DateTime to)
        {
            var samples = await ctx.PowerMeterTelemetries.AsNoTracking()
                .Where(t => t.Timestamp >= from && t.Timestamp < to && t.PowerW != null)
                .OrderBy(t => t.Timestamp)
                .Select(t => new { t.Timestamp, t.PowerW })
                .ToListAsync(ct);
            if (samples.Count < 2) return 0d;
            double wattSeconds = 0;
            for (int i = 1; i < samples.Count; i++)
            {
                var prev = samples[i - 1];
                var curr = samples[i];
                var dtSeconds = (curr.Timestamp - prev.Timestamp).TotalSeconds;
                if (dtSeconds <= 0 || dtSeconds > 3600) continue; // skip gap
                double avgW = ((prev.PowerW ?? 0) + (curr.PowerW ?? 0)) / 2.0;
                wattSeconds += avgW * dtSeconds;
            }
            return wattSeconds / 3600.0 / 1000.0; // kWh
        }

        var dayKWh = await ComputeEnergyKWh(dayStart, now);
        var monthKWh = await ComputeEnergyKWh(monthStart, now);

        // Tariffa default
        var tariff = await ctx.ElectricityTariffs.AsNoTracking()
            .Where(t => t.IsDefault)
            .OrderByDescending(t => t.ValidFromUtc)
            .FirstOrDefaultAsync(ct);

        double? dayCost = null; double? monthCost = null; string? tariffName = null;
        if (tariff != null)
        {
            tariffName = tariff.Name;
            double f1 = tariff.CostF1;
            // Per semplicità assumiamo tutta l'energia in F1 (manca classificazione fasce orarie). Se IsTimeOfUse si potrebbe future-split.
            dayCost = dayKWh * f1;
            monthCost = monthKWh * f1;
        }

        return Ok(new DashboardConsumptionDto(dayStart, monthStart, now, dayKWh, monthKWh, dayCost, monthCost, tariffName));
    }
}
