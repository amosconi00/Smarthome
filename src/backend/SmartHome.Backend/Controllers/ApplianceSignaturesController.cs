using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Dto.Signatures;
using SmartHome.Backend.Dto.Appliances;
using SmartHome.Backend.Services;
using Microsoft.AspNetCore.Http;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/appliance-signatures")] // collection
public class ApplianceSignaturesController : ControllerBase
{
    private readonly IApplianceSignatureService _service;

    public ApplianceSignaturesController(IApplianceSignatureService service)
    {
        _service = service;
    }

    // GET all signatures
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ApplianceSignatureDto>>> GetAll(CancellationToken ct)
    {
        var list = await _service.GetAllAsync(ct);
        return Ok(list);
    }

    // GET signatures for appliance
    [HttpGet("appliance/{applianceId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ApplianceSignatureDto>>> GetForAppliance(int applianceId, CancellationToken ct)
    {
        var list = await _service.GetByApplianceAsync(applianceId, ct);
        return Ok(list);
    }

    // GET default for appliance
    [HttpGet("appliance/{applianceId:int}/default")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplianceSignatureDto>> GetDefault(int applianceId, CancellationToken ct)
    {
        var sig = await _service.GetDefaultAsync(applianceId, ct);
        if (sig is null) return NotFound();
        return Ok(sig);
    }

    // GET single by id
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplianceSignatureDto>> Get(int id, CancellationToken ct)
    {
        var sig = await _service.GetByIdAsync(id, ct);
        if (sig is null) return NotFound();
        return Ok(sig);
    }

    // POST create (applianceId in route)
    [HttpPost("appliance/{applianceId:int}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApplianceSignatureDto>> Create(int applianceId, [FromBody] ApplianceSignatureCreateUpdateDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        var created = await _service.CreateAsync(applianceId, dto, ct);
        if (created is null) return NotFound(); // appliance missing
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    // PUT update
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ApplianceSignatureCreateUpdateDto dto, CancellationToken ct)
    {
        var ok = await _service.UpdateAsync(id, dto, ct);
        if (!ok) return NotFound();
        return NoContent();
    }

    // DELETE
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(id, ct);
        if (!ok) return NotFound();
        return NoContent();
    }
}
