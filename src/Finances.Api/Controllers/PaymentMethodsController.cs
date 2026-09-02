using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodService _service;

    public PaymentMethodsController(IPaymentMethodService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentMethodDto>>> GetAll(
        [FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(includeArchived, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentMethodDto>> GetById(int id, CancellationToken ct)
    {
        var method = await _service.GetByIdAsync(id, ct);
        return method is null ? NotFound() : Ok(method);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentMethodDto>> Create(PaymentMethodCreateDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PaymentMethodDto>> Update(int id, PaymentMethodCreateDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
