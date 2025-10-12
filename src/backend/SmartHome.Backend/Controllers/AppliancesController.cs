using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Dto.Appliances;
using SmartHome.Backend.Services;
using Microsoft.AspNetCore.Http;

namespace SmartHome.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppliancesController : ControllerBase
    {
        private readonly IApplianceService _service;

        public AppliancesController(IApplianceService service)
        {
            _service = service;
        }

        // GET: api/appliances
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ApplianceDto>>> GetAll(CancellationToken cancellationToken)
        {
            var items = await _service.GetAllAsync(cancellationToken);
            return Ok(items);
        }

        // GET: api/appliances/{id}
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApplianceDto>> GetById(int id, CancellationToken cancellationToken)
        {
            var applianceDto = await _service.GetByIdAsync(id, cancellationToken);
            if (applianceDto is null)
                return NotFound();
            return Ok(applianceDto);
        }

        // POST: api/appliances
        [HttpPost]
        [ProducesResponseType(typeof(ApplianceDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApplianceDto>> Create([FromBody] ApplianceCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            if (dto is null)
                return BadRequest();

            var result = await _service.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        // PUT: api/appliances/{id}
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] ApplianceCreateUpdateDto dto, CancellationToken cancellationToken)
        {
            if (dto is null)
                return BadRequest();

            var ok = await _service.UpdateAsync(id, dto, cancellationToken);
            if (!ok)
                return NotFound();

            return NoContent();
        }

        // DELETE: api/appliances/{id}
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var ok = await _service.DeleteAsync(id, cancellationToken);
            if (!ok)
                return NotFound();

            return NoContent();
        }
    }
}
