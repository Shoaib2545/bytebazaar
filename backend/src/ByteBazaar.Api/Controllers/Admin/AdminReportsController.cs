using System.Text;
using ByteBazaar.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = "Admin,Staff")]
public class AdminReportsController : ControllerBase
{
    private readonly ReportService _service;

    public AdminReportsController(ReportService service)
    {
        _service = service;
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSales(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] string? groupBy = "day", [FromQuery] string? format = null,
        CancellationToken ct = default)
    {
        var rows = await _service.GetSalesAsync(from, to, groupBy, ct);
        if (!IsCsv(format)) return Ok(rows);
        return Csv("sales-report.csv", CsvWriter.Write(
            new[] { "period", "orders", "revenue" },
            rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.Period, r.Orders, r.Revenue })));
    }

    [HttpGet("by-category")]
    public async Task<IActionResult> GetByCategory(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] string? format = null, CancellationToken ct = default)
    {
        var rows = await _service.GetByCategoryAsync(from, to, ct);
        if (!IsCsv(format)) return Ok(rows);
        return Csv("sales-by-category.csv", CsvWriter.Write(
            new[] { "categoryName", "orders", "units", "revenue" },
            rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.CategoryName, r.Orders, r.Units, r.Revenue })));
    }

    [HttpGet("by-brand")]
    public async Task<IActionResult> GetByBrand(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] string? format = null, CancellationToken ct = default)
    {
        var rows = await _service.GetByBrandAsync(from, to, ct);
        if (!IsCsv(format)) return Ok(rows);
        return Csv("sales-by-brand.csv", CsvWriter.Write(
            new[] { "brandName", "orders", "units", "revenue" },
            rows.Select(r => (IReadOnlyList<object?>)new object?[] { r.BrandName, r.Orders, r.Units, r.Revenue })));
    }

    private static bool IsCsv(string? format) => string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

    private FileContentResult Csv(string fileName, string content)
        => File(Encoding.UTF8.GetBytes(content), "text/csv", fileName);
}
