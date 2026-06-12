using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserAdminService _admin;

    public AdminController(IUserAdminService admin) => _admin = admin;

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default) =>
        Ok(await _admin.GetUsersAsync(new UserFilter(search, role, from, to, page, pageSize), ct));

    [HttpGet("users/{id}")]
    public async Task<ActionResult<AdminUserDto>> GetUser(string id, CancellationToken ct) =>
        Ok(await _admin.GetUserAsync(id, ct));

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats(CancellationToken ct) =>
        Ok(await _admin.GetStatsAsync(ct));

    [HttpGet("reports/export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var csv = await _admin.ExportUsersCsvAsync(new UserFilter(search, role, from, to, 1, int.MaxValue), ct);
        return File(csv, "text/csv", $"users-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
