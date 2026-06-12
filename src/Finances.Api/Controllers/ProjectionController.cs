using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectionController : ControllerBase
{
    private readonly IProjectionService _service;

    public ProjectionController(IProjectionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ProjectionDto>> Get(
        [FromQuery] decimal savingsRate = 0.20m,
        [FromQuery] int historyMonths = 6,
        [FromQuery] string lang = "en",
        CancellationToken ct = default)
    {
        if (savingsRate is < 0m or > 0.9m)
            throw new ValidationException("savingsRate debe estar entre 0 y 0.9");
        if (historyMonths is < 2 or > 24)
            throw new ValidationException("historyMonths debe estar entre 2 y 24");

        return Ok(await _service.BuildProjectionAsync(savingsRate, historyMonths, lang, ct));
    }
}
