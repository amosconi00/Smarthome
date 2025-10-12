using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Services;
using SmartHome.Backend.Dto.Signatures;
using SmartHome.Backend.Extensions;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/appliance-signatures/generation")] // grouping under signatures
public class ApplianceSignatureGenerationController : ControllerBase
{
    private readonly IApplianceSignatureGenerationService _generator;

    public ApplianceSignatureGenerationController(IApplianceSignatureGenerationService generator)
    {
        _generator = generator;
    }

    /// <summary>
    /// Generate a new signature for the specified appliance based on historical telemetry.
    /// </summary>
    /// <param name="applianceId">Target appliance id.</param>
    /// <param name="fromUtc">Optional start (UTC) of the telemetry interval.</param>
    /// <param name="toUtc">Optional end (UTC) of the telemetry interval.</param>
    /// <param name="setAsDefault">If true sets the generated signature as default (unsetting previous one).</param>
    [HttpPost("{applianceId:int}")]
    [ProducesResponseType(typeof(ApplianceSignatureDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generate(
        int applianceId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] bool setAsDefault,
        CancellationToken ct)
    {
        if (toUtc.HasValue && fromUtc.HasValue && toUtc < fromUtc)
            return BadRequest("toUtc must be >= fromUtc");
        fromUtc = fromUtc.Value.EnsureUtc();
        toUtc = toUtc.Value.EnsureUtc();
        var sig = await _generator.GenerateAsync(applianceId, fromUtc.Value.EnsureUtc(), toUtc.Value.EnsureUtc(), setAsDefault, ct);
        if (sig is null)
            return NotFound();

        return CreatedAtRoute("GetApplianceSignatureById", new { id = sig.Id }, sig);
    }
}
