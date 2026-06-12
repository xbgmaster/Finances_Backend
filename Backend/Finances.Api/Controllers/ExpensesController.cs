using Finances.Api.Data;
using Finances.Api.Dtos;
using Finances.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finances.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly FinanceDbContext _db;

    public ExpensesController(FinanceDbContext db) => _db = db;

    /// <summary>Lista los gastos, opcionalmente filtrados por anio/mes.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetAll([FromQuery] int? year, [FromQuery] int? month)
    {
        var query = _db.Expenses.Include(e => e.Category).AsQueryable();

        if (year is not null) query = query.Where(e => e.Date.Year == year);
        if (month is not null) query = query.Where(e => e.Date.Month == month);

        var items = await query
            .OrderByDescending(e => e.Date)
            .Select(e => new ExpenseDto(
                e.Id, e.Amount, e.Description, e.Date,
                e.CategoryId,
                e.Category!.Name, e.Category.Icon, e.Category.Color,
                e.ReceiptUrl))
            .ToListAsync();
        return Ok(items);
    }

    private static readonly string[] AllowedImageTypes =
        { "image/jpeg", "image/png", "image/webp", "image/gif" };
    private const long MaxReceiptBytes = 5 * 1024 * 1024; // 5 MB

    /// <summary>
    /// Crea un gasto. Acepta multipart/form-data para permitir adjuntar
    /// opcionalmente la imagen de la factura/recibo en el campo "receipt".
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ExpenseDto>> Create(
        [FromForm] ExpenseCreateDto dto,
        IFormFile? receipt,
        [FromServices] IWebHostEnvironment env)
    {
        var category = await _db.Categories.FindAsync(dto.CategoryId);
        if (category is null)
            return BadRequest(new { message = "La categoria indicada no existe." });

        string? receiptUrl = null;
        if (receipt is { Length: > 0 })
        {
            if (receipt.Length > MaxReceiptBytes)
                return BadRequest(new { message = "La imagen supera el tamano maximo de 5 MB." });
            if (!AllowedImageTypes.Contains(receipt.ContentType))
                return BadRequest(new { message = "Formato de imagen no permitido (usa JPG, PNG, WEBP o GIF)." });

            var uploadsDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(receipt.FileName);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsDir, fileName);
            await using (var stream = System.IO.File.Create(fullPath))
            {
                await receipt.CopyToAsync(stream);
            }
            receiptUrl = $"/uploads/{fileName}";
        }

        var expense = new Expense
        {
            Amount = dto.Amount,
            Description = dto.Description?.Trim() ?? string.Empty,
            Date = dto.Date ?? DateTime.UtcNow,
            CategoryId = dto.CategoryId,
            ReceiptUrl = receiptUrl
        };
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        var result = new ExpenseDto(
            expense.Id, expense.Amount, expense.Description, expense.Date,
            category.Id, category.Name, category.Icon, category.Color,
            expense.ReceiptUrl);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromServices] IWebHostEnvironment env)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense is null) return NotFound();

        // Elimina tambien el archivo de recibo asociado, si existe.
        if (!string.IsNullOrEmpty(expense.ReceiptUrl))
        {
            var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            var filePath = Path.Combine(webRoot, expense.ReceiptUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
