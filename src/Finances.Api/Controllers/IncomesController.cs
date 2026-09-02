using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncomesController : ControllerBase
{
    private readonly IIncomeService _service;

    public IncomesController(IIncomeService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<IncomeDto>>> GetAll([FromQuery] string? currency, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(currency, ct));

    [HttpPost]
    public async Task<ActionResult<IncomeDto>> Create(IncomeCreateDto dto, CancellationToken ct) =>
        Ok(await _service.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<IncomeDto>> Update(int id, IncomeUpdateDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
