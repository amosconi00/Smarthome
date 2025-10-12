using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Services;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private readonly IExportService _service;

    public ExportController(IExportService service)
    {
        _service = service;
    }

    // GET api/export/powermeter?powerMeterId=1&from=...&to=...
    [HttpGet("powermeter")]
    public async Task<IActionResult> ExportPowerMeter([FromQuery] int powerMeterId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var (fileName, content) = await _service.ExportPowerMeterAsync(powerMeterId, from, to, cancellationToken);
        return File(content, "text/csv", fileName);
    }

    // GET api/export/appliance?applianceId=1&from=...&to=...
    [HttpGet("appliance")]
    public async Task<IActionResult> ExportAppliance([FromQuery] int applianceId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var (fileName, content) = await _service.ExportApplianceAsync(applianceId, from, to, cancellationToken);
        return File(content, "text/csv", fileName);
    }
}
