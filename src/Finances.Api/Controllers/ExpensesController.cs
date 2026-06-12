using Finances.Application.Common;
using Finances.Application.Dtos;
using Finances.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _service;

    public ExpensesController(IExpenseService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetAll(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(year, month, ct));

    /// <summary>
    /// Crea un gasto. Acepta multipart/form-data para adjuntar opcionalmente
    /// la imagen de la factura/recibo en el campo "receipt".
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ExpenseDto>> Create(
        [FromForm] ExpenseCreateDto dto, IFormFile? receipt, CancellationToken ct)
    {
        FileUpload? upload = null;
        if (receipt is { Length: > 0 })
            upload = new FileUpload(receipt.OpenReadStream(), receipt.FileName, receipt.ContentType, receipt.Length);

        var created = await _service.CreateAsync(dto, upload, ct);
        return Ok(created);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
