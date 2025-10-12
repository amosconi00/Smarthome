using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Services;
using SmartHome.Backend.Dto.Nilm;
using SmartHome.Backend.Extensions;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NilmController : ControllerBase
{
    private readonly INilmService _service;
    public NilmController(INilmService service)
    {
        _service = service;
    }

    [HttpGet("appliance/series")]
    [ProducesResponseType(typeof(NilmSeriesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NilmSeriesDto>> GetSeries(
        [FromQuery] int applianceId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken ct)
    {
        if (applianceId <= 0) return BadRequest("applianceId obbligatorio");
        fromUtc = fromUtc.EnsureUtc();
        toUtc = toUtc.EnsureUtc();
        if (toUtc <= fromUtc) return BadRequest("Intervallo non valido");
        try { return Ok(await _service.GetSeriesCompareAsync(applianceId, fromUtc, toUtc, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("appliance/aligned")]
    [ProducesResponseType(typeof(IEnumerable<NilmAlignedPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<NilmAlignedPointDto>>> GetAligned(
        [FromQuery] int applianceId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] int size = 1,
        [FromQuery] string unit = "second",
        CancellationToken ct = default)
    {
        if (applianceId <= 0) return BadRequest("applianceId obbligatorio");
        fromUtc = fromUtc.EnsureUtc();
        toUtc = toUtc.EnsureUtc();
        if (toUtc <= fromUtc) return BadRequest("Intervallo non valido");
        try { return Ok(await _service.GetSeriesDownsampledAsync(applianceId, fromUtc, toUtc, size, unit, ct)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("appliance/recompute")]
    [ProducesResponseType(typeof(NilmSeriesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NilmSeriesDto>> Recompute(
        [FromQuery] int applianceId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken ct)
    {
        if (applianceId <= 0) return BadRequest("applianceId obbligatorio");
        fromUtc = fromUtc.EnsureUtc();
        toUtc = toUtc.EnsureUtc();
        if (toUtc <= fromUtc) return BadRequest("Intervallo non valido");
        try { return Ok(await _service.RecomputeFromMainsAsync(applianceId, fromUtc, toUtc, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("appliance/recomputewithsignature")]
    [ProducesResponseType(typeof(NilmSeriesHartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NilmSeriesHartDto>> RecomputeWithSignature(
        [FromQuery] int applianceId,
        [FromQuery] int signatureId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken ct)
    {
        if (applianceId <= 0) return BadRequest("applianceId obbligatorio");
        fromUtc = fromUtc.EnsureUtc();
        toUtc = toUtc.EnsureUtc();
        if (toUtc <= fromUtc) return BadRequest("Intervallo non valido");
        try { return Ok(await _service.RecomputeHartFromMainsAsync(applianceId, signatureId, fromUtc, toUtc, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("appliance/recomputewithsignature/whatif")]
    [ProducesResponseType(typeof(NilmSeriesHartDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NilmSeriesHartDto>> RecomputeWithSignatureWhatIf(
        [FromQuery] int applianceId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromBody] NilmWhatIfSignatureDto signature,
        CancellationToken ct)
    {
        if (applianceId <= 0) return BadRequest("applianceId obbligatorio");
        fromUtc = fromUtc.EnsureUtc();
        toUtc = toUtc.EnsureUtc();
        if (toUtc <= fromUtc) return BadRequest("Intervallo non valido");
        if (signature is null) return BadRequest("Signature mancante");
        try { return Ok(await _service.RecomputeHartWhatIfAsync(applianceId, signature, fromUtc, toUtc, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
