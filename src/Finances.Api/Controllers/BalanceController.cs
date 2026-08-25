using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BalanceController : ControllerBase
{
    private readonly IBalanceService _service;

    public BalanceController(IBalanceService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<BalanceDto>> GetBalance(CancellationToken ct) =>
        Ok(await _service.GetBalanceAsync(ct));

    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlySummaryDto>> GetMonthly(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) =>
        Ok(await _service.GetMonthlyAsync(year, month, ct));

    [HttpGet("budget-history")]
    public async Task<ActionResult<BudgetHistoryDto>> GetBudgetHistory(
        [FromQuery] int months, CancellationToken ct) =>
        Ok(await _service.GetBudgetHistoryAsync(months <= 0 ? 6 : months, ct));
}
