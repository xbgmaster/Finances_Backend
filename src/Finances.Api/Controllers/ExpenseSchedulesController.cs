using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/expense-schedules")]
public class ExpenseSchedulesController : ControllerBase
{
    private readonly IExpenseScheduleService _service;

    public ExpenseSchedulesController(IExpenseScheduleService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseScheduleDto>>> GetAll(CancellationToken ct) =>
        Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ExpenseScheduleDto>> Create(ExpenseScheduleCreateDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ExpenseScheduleDto>> Update(int id, ExpenseScheduleUpdateDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("post-due")]
    public async Task<ActionResult<object>> PostDue(CancellationToken ct) =>
        Ok(new { posted = await _service.PostDueForCurrentUserAsync(ct) });
}
