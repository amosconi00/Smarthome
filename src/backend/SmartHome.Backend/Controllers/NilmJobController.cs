using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.BackgroundServices;
using SmartHome.Backend.Extensions;
using Microsoft.AspNetCore.Http; // per StatusCodes
using SmartHome.Backend.Services;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/nilmjob")] 
[Produces("application/json")]
public class NilmJobController : ControllerBase
{
    private readonly NilmJobBackgroundService _jobBackgroundService;
    private readonly ILogger<NilmJobController> _logger;
    private readonly INilmModeService _modeService;

    public NilmJobController(IEnumerable<IHostedService> hostedServices, ILogger<NilmJobController> logger, INilmModeService modeService)
    {
        _jobBackgroundService = hostedServices.OfType<NilmJobBackgroundService>().Single();
        _logger = logger;
        _modeService = modeService;
    }

    public sealed record RunRequest(DateTime StartUtc, DateTime EndUtc);

    [HttpGet("status")] 
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> Status()
    {
        var st = _jobBackgroundService.GetStatus();
        return Ok(new { running = st.Running, startUtc = st.StartUtc, endUtc = st.EndUtc, mode = _modeService.CurrentMode.ToString() });
    }

    [HttpPost("run")] 
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<object> Run([FromBody] RunRequest req, CancellationToken ct)
    {
        if (_modeService.CurrentMode == NilmMode.Realtime) return Conflict("Current mode is Realtime: switch to NightlyBatch to run the job");
        if (req.EndUtc <= req.StartUtc) return BadRequest("EndUtc must be greater than StartUtc");
        try
        {
            var ok = _jobBackgroundService.TryRun(req.StartUtc.EnsureUtc(), req.EndUtc.EnsureUtc(), ct);
            if (!ok) return Conflict("Job already running or not allowed");
            return Accepted(new { queued = true, startUtc = req.StartUtc, endUtc = req.EndUtc });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting NilmJobBackgroundService");
            return StatusCode(500, ex.Message);
        }
    }
}
