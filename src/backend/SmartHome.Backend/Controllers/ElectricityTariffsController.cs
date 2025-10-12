using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Services;
using SmartHome.Backend.Dto.Tariffs;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ElectricityTariffsController : ControllerBase
{
    private readonly IElectricityTariffService _service;
    public ElectricityTariffsController(IElectricityTariffService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ElectricityTariffDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ElectricityTariffDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ElectricityTariffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ElectricityTariffDto>> GetById(int id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ElectricityTariffDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ElectricityTariffDto>> Create([FromBody] ElectricityTariffCreateUpdateDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ElectricityTariffCreateUpdateDto dto, CancellationToken ct)
    {
        var ok = await _service.UpdateAsync(id, dto, ct);
        if (!ok) return NotFound();
        return NoContent();
    }

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
