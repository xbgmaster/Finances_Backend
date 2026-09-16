using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/income-schedules")]
// Any authenticated user: this is an opt-in module the admin grants per user from
// "Feature access". Everything here is user-scoped (each user only sees their own jobs);
// the front-end hides it unless granted. Per-feature backend enforcement can be added later.
[Authorize]
public class IncomeSchedulesController : ControllerBase
{
    private readonly IIncomeScheduleService _service;

    public IncomeSchedulesController(IIncomeScheduleService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncomeScheduleDto>>> GetAll(CancellationToken ct) =>
        Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<IncomeScheduleDto>> Create([FromBody] IncomeScheduleCreateDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<IncomeScheduleDto>> Update(int id, [FromBody] IncomeScheduleUpdateDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Posts any due pay for the current user's schedules right now (catch-up on demand).</summary>
    [HttpPost("post-due")]
    public async Task<ActionResult<object>> PostDue(CancellationToken ct)
    {
        var posted = await _service.PostDueForCurrentUserAsync(ct);
        return Ok(new { posted });
    }

    // ---- Work shifts (hourly jobs) ------------------------------------------------------------

    [HttpGet("shifts")]
    public async Task<ActionResult<IReadOnlyList<WorkShiftDto>>> GetShifts([FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        Ok(await _service.GetShiftsAsync(year, month, ct));

    [HttpPost("shifts")]
    public async Task<ActionResult<WorkShiftDto>> CreateShift([FromBody] WorkShiftCreateDto dto, CancellationToken ct) =>
        Ok(await _service.CreateShiftAsync(dto, ct));

    [HttpPut("shifts/{id:int}")]
    public async Task<ActionResult<WorkShiftDto>> UpdateShift(int id, [FromBody] WorkShiftUpdateDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateShiftAsync(id, dto, ct));

    [HttpDelete("shifts/{id:int}")]
    public async Task<IActionResult> DeleteShift(int id, CancellationToken ct)
    {
        await _service.DeleteShiftAsync(id, ct);
        return NoContent();
    }

    /// <summary>Posts a direct one-off payment (income) attributed to a job on a given day.</summary>
    [HttpPost("{id:int}/payments")]
    public async Task<IActionResult> CreatePayment(int id, [FromBody] WorkPaymentCreateDto dto, CancellationToken ct)
    {
        await _service.CreatePaymentAsync(id, dto, ct);
        return NoContent();
    }
}
