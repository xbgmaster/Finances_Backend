using Finances.Application.Credits.Commands.CreateCredit;
using Finances.Application.Credits.Commands.DeleteCredit;
using Finances.Application.Credits.Commands.DeletePayment;
using Finances.Application.Credits.Commands.RegisterPayment;
using Finances.Application.Credits.Commands.UpdatePayment;
using Finances.Application.Credits.Queries.GetAmortizationSchedule;
using Finances.Application.Credits.Queries.GetCreditPayments;
using Finances.Application.Credits.Queries.GetCreditSummary;
using Finances.Application.Credits.Queries.GetCredits;
using Finances.Application.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CreditsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CreditsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CreditDto>>> GetAll(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetCreditsQuery(), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CreditSummaryDto>> GetById(int id, CancellationToken ct)
    {
        var summary = await _mediator.Send(new GetCreditSummaryQuery(id), ct);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("{id:int}/schedule")]
    public async Task<ActionResult<CreditScheduleDto>> GetSchedule(int id, CancellationToken ct)
    {
        var schedule = await _mediator.Send(new GetAmortizationScheduleQuery(id), ct);
        return schedule is null ? NotFound() : Ok(schedule);
    }

    [HttpPost]
    public async Task<ActionResult<CreditDto>> Create(CreateCreditCommand command, CancellationToken ct)
    {
        var created = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:int}/payments")]
    public async Task<ActionResult<IEnumerable<CreditPaymentDto>>> GetPayments(int id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetCreditPaymentsQuery(id), ct));

    [HttpPost("{id:int}/payments")]
    public async Task<ActionResult<CreditSummaryDto>> RegisterPayment(
        int id, RegisterPaymentCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command with { CreditId = id }, ct));

    [HttpPut("{id:int}/payments/{paymentId:int}")]
    public async Task<ActionResult<CreditSummaryDto>> UpdatePayment(
        int id, int paymentId, UpdatePaymentCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command with { CreditId = id, PaymentId = paymentId }, ct));

    [HttpDelete("{id:int}/payments/{paymentId:int}")]
    public async Task<IActionResult> DeletePayment(int id, int paymentId, CancellationToken ct)
    {
        await _mediator.Send(new DeletePaymentCommand(id, paymentId), ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteCreditCommand(id), ct);
        return NoContent();
    }
}
