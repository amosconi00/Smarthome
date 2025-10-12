using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Services;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/nilm/mode")] 
public class NilmModeController : ControllerBase
{
    private readonly INilmModeService _modeService;
    private readonly ILogger<NilmModeController> _logger;

    public NilmModeController(INilmModeService modeService, ILogger<NilmModeController> logger)
    {
        _modeService = modeService;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<object> Get() => Ok(new { mode = _modeService.CurrentMode.ToString() });

    public sealed record SetModeRequest(string Mode);

    [HttpPost]
    public ActionResult<object> Set([FromBody] SetModeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Mode)) return BadRequest("Mode required");
        if (!Enum.TryParse<NilmMode>(req.Mode, true, out var mode)) return BadRequest("Invalid mode");
        var changed = _modeService.SetMode(mode);
        return Ok(new { mode = _modeService.CurrentMode.ToString(), changed });
    }
}
