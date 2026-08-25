using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExchangesController : ControllerBase
{
    private readonly IExchangeService _service;

    public ExchangesController(IExchangeService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExchangeDto>>> GetAll(CancellationToken ct) =>
        Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ExchangeDto>> Create(ExchangeCreateDto dto, CancellationToken ct) =>
        Ok(await _service.CreateAsync(dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
