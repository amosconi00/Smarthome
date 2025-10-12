using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Services;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/nilm/batch")] 
public class NilmBatchController : ControllerBase
{
    private readonly INilmBatchService _batch;
    private readonly ILogger<NilmBatchController> _logger;

    public NilmBatchController(INilmBatchService batch, ILogger<NilmBatchController> logger)
    {
        _batch = batch;
        _logger = logger;
    }

    public sealed record RunRequest(DateTime StartUtc, DateTime EndUtc);

    [HttpGet("run")]
    public async Task<ActionResult<object>> Run([FromBody] RunRequest req, CancellationToken ct)
    {
        if (req.EndUtc <= req.StartUtc) return BadRequest("EndUtc deve essere > StartUtc");
        try
        {
            var res = await _batch.ProcessAsync(req.StartUtc, req.EndUtc, ct);
            return Ok(new
            {
                res.StartUtc,
                res.EndUtc,
                res.TelemetriesProcessed,
                res.DetectionsInserted,
                res.ModelsLoaded,
                res.DeletedExisting,
                res.SecondsElapsed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore esecuzione batch NILM");
            return StatusCode(500, ex.Message);
        }
    }
}
