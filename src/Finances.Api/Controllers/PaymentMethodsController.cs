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

    [HttpPut("{id:int}/favorite")]
    public async Task<ActionResult<PaymentMethodDto>> SetFavorite(
        int id, [FromQuery] bool value, CancellationToken ct) =>
        Ok(await _service.SetFavoriteAsync(id, value, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/payments")]
    public async Task<ActionResult<IEnumerable<CardPaymentDto>>> GetPayments(int id, CancellationToken ct) =>
        Ok(await _service.GetCardPaymentsAsync(id, ct));

    [HttpPost("{id:int}/payments")]
    public async Task<ActionResult<CardPaymentDto>> PayCard(int id, CardPaymentCreateDto dto, CancellationToken ct) =>
        Ok(await _service.PayCardAsync(id, dto, ct));

    [HttpDelete("{id:int}/payments/{paymentId:int}")]
    public async Task<IActionResult> DeletePayment(int id, int paymentId, CancellationToken ct)
    {
        await _service.DeleteCardPaymentAsync(paymentId, ct);
        return NoContent();
    }
}
