using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Dto.PowerMeters;
using SmartHome.Backend.Services;
using Microsoft.AspNetCore.Http;

namespace SmartHome.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PowerMetersController : ControllerBase
    {
        private readonly IPowerMeterService _service;

        public PowerMetersController(IPowerMeterService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PowerMeterDto>>> GetAll(CancellationToken cancellationToken)
        {
            var items = await _service.GetAllAsync(cancellationToken);
            return Ok(items);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PowerMeterDto>> GetById(int id, CancellationToken cancellationToken)
        {
            var dto = await _service.GetByIdAsync(id, cancellationToken);
            if (dto is null) return NotFound();
            return Ok(dto);
        }

        [HttpPost]
        [ProducesResponseType(typeof(PowerMeterDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PowerMeterDto>> Create([FromBody] PowerMeterCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            if (dto is null) return BadRequest();
            var created = await _service.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] PowerMeterCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            if (dto is null) return BadRequest();
            var ok = await _service.UpdateAsync(id, dto, cancellationToken);
            if (!ok) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var ok = await _service.DeleteAsync(id, cancellationToken);
            if (!ok) return NotFound();
            return NoContent();
        }
    }
}
