using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Dal;
using SmartHome.Backend.Dto.Consumption;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsumptionController : ControllerBase
{
    private readonly IDbContextFactory<SmartHomeDbContext> _ctxFactory;
    public ConsumptionController(IDbContextFactory<SmartHomeDbContext> ctxFactory)
    {
        _ctxFactory = ctxFactory;
    }

    /// <summary>
    /// Consumi giornalieri (kWh) per un power meter nel mese/anno specificati, con totale.
    /// </summary>
    [HttpGet("powermeter/{powerMeterId:int}/monthly")]
    [ProducesResponseType(typeof(MonthlyConsumptionsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyConsumptionsResponseDto>> GetPowerMeterMonthly(int powerMeterId, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12) return BadRequest("Month 1-12");
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var days = await ctx.PowerMeterDailyConsumptions.AsNoTracking()
            .Where(v => v.PowerMeterId == powerMeterId && v.Day.Year == year && v.Day.Month == month)
            .OrderBy(v => v.Day)
            .Select(v => new DailyConsumptionDto(v.Day, v.EnergyKWh))
            .ToListAsync(ct);
        var total = days.Sum(d => d.EnergyKWh);
        return Ok(new MonthlyConsumptionsResponseDto(year, month, powerMeterId, days, total));
    }

    /// <summary>
    /// Solo lista consumi giornalieri (kWh) per power meter nel mese/anno.
    /// </summary>
    [HttpGet("powermeter/{powerMeterId:int}/daily")]
    [ProducesResponseType(typeof(IEnumerable<DailyConsumptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DailyConsumptionDto>>> GetPowerMeterDaily(int powerMeterId, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12) return BadRequest("Month 1-12");
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var days = await ctx.PowerMeterDailyConsumptions.AsNoTracking()
            .Where(v => v.PowerMeterId == powerMeterId && v.Day.Year == year && v.Day.Month == month)
            .OrderBy(v => v.Day)
            .Select(v => new DailyConsumptionDto(v.Day, v.EnergyKWh))
            .ToListAsync(ct);
        return Ok(days);
    }

    /// <summary>
    /// Consumi giornalieri (kWh) per un appliance nel mese/anno specificati, con totale.
    /// </summary>
    [HttpGet("appliance/{applianceId:int}/monthly")]
    [ProducesResponseType(typeof(MonthlyConsumptionsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyConsumptionsResponseDto>> GetApplianceMonthly(int applianceId, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12) return BadRequest("Month 1-12");
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var days = await ctx.ApplianceDailyConsumptions.AsNoTracking()
            .Where(v => v.ApplianceId == applianceId && v.Day.Year == year && v.Day.Month == month)
            .OrderBy(v => v.Day)
            .Select(v => new DailyConsumptionDto(v.Day, v.EnergyKWh))
            .ToListAsync(ct);
        var total = days.Sum(d => d.EnergyKWh);
        return Ok(new MonthlyConsumptionsResponseDto(year, month, applianceId, days, total));
    }

    /// <summary>
    /// Solo lista consumi giornalieri (kWh) per appliance nel mese/anno.
    /// </summary>
    [HttpGet("appliance/{applianceId:int}/daily")]
    [ProducesResponseType(typeof(IEnumerable<DailyConsumptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DailyConsumptionDto>>> GetApplianceDaily(int applianceId, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month < 1 || month > 12) return BadRequest("Month 1-12");
        await using var ctx = await _ctxFactory.CreateDbContextAsync(ct);
        var days = await ctx.ApplianceDailyConsumptions.AsNoTracking()
            .Where(v => v.ApplianceId == applianceId && v.Day.Year == year && v.Day.Month == month)
            .OrderBy(v => v.Day)
            .Select(v => new DailyConsumptionDto(v.Day, v.EnergyKWh))
            .ToListAsync(ct);
        return Ok(days);
    }
}
