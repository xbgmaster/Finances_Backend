using Finances.Api.Dtos;
using Finances.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectionController : ControllerBase
{
    private readonly IProjectionService _projection;

    public ProjectionController(IProjectionService projection) => _projection = projection;

    /// <summary>
    /// Devuelve la proyeccion financiera: gasto estimado del proximo mes,
    /// ahorro recomendado, cuanto se puede gastar de forma segura e insights.
    /// </summary>
    /// <param name="savingsRate">Meta de ahorro (0-1). Por defecto 0.20 (20%).</param>
    /// <param name="historyMonths">Meses de historico a considerar (por defecto 6).</param>
    [HttpGet]
    public async Task<ActionResult<ProjectionDto>> Get(
        [FromQuery] decimal savingsRate = 0.20m,
        [FromQuery] int historyMonths = 6,
        [FromQuery] string lang = "en",
        CancellationToken ct = default)
    {
        if (savingsRate < 0m || savingsRate > 0.9m)
            return BadRequest(new { message = "savingsRate debe estar entre 0 y 0.9" });
        if (historyMonths is < 2 or > 24)
            return BadRequest(new { message = "historyMonths debe estar entre 2 y 24" });

        var result = await _projection.BuildProjectionAsync(savingsRate, historyMonths, lang, ct);
        return Ok(result);
    }
}
