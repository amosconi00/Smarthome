using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Dto.CategoryAppliances;
using SmartHome.Backend.Dto.Signatures;
using SmartHome.Backend.Services;
using Microsoft.AspNetCore.Http;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/category-appliance-signatures")] // collection route
public class CategoryApplianceSignaturesController : ControllerBase
{
    private readonly ICategoryApplianceSignatureService _service;

    public CategoryApplianceSignaturesController(ICategoryApplianceSignatureService service)
    {
        _service = service;
    }

    // GET collection
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryApplianceSignatureDto>>> GetAll(CancellationToken ct)
    {
        var list = await _service.GetAllAsync(ct);
        return Ok(list);
    }

    // GET single
    [HttpGet("{categoryApplianceId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryApplianceSignatureDto>> Get(int categoryApplianceId, CancellationToken ct)
    {
        var sig = await _service.GetByIdAsync(categoryApplianceId, ct);
        if (sig is null) return NotFound();
        return Ok(sig);
    }

    // POST create
    [HttpPost("{categoryApplianceId:int}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryApplianceSignatureDto>> Create(int categoryApplianceId, [FromBody] CategoryApplianceSignatureCreateUpdateDto dto, CancellationToken ct)
    {
        if (dto is null) return BadRequest();
        var created = await _service.CreateAsync(categoryApplianceId, dto, ct);
        if (created is null) return Conflict();
        return CreatedAtAction(nameof(Get), new { categoryApplianceId }, created);
    }

    // PUT update
    [HttpPut("{categoryApplianceId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int categoryApplianceId, [FromBody] CategoryApplianceSignatureCreateUpdateDto dto, CancellationToken ct)
    {
        var ok = await _service.UpdateAsync(categoryApplianceId, dto, ct);
        if (!ok) return NotFound();
        return NoContent();
    }

    // DELETE
    [HttpDelete("{categoryApplianceId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int categoryApplianceId, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(categoryApplianceId, ct);
        if (!ok) return NotFound();
        return NoContent();
    }
}
