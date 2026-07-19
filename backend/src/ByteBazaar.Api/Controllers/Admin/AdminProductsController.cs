using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin,Staff")]
public class AdminProductsController : ControllerBase
{
    private readonly AdminCatalogService _service;
    private readonly IValidator<ProductUpsertRequest> _validator;

    public AdminProductsController(AdminCatalogService service, IValidator<ProductUpsertRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<AdminProductListItemDto>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] Guid? categoryId = null,
        CancellationToken ct = default)
        => Ok(await _service.GetProductsAsync(page, pageSize, search, categoryId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminProductDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var product = await _service.GetProductAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<AdminProductDetailDto>> Create([FromBody] ProductUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        return Ok(await _service.CreateProductAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminProductDetailDto>> Update(Guid id, [FromBody] ProductUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        var updated = await _service.UpdateProductAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _service.DeleteProductAsync(id, ct) ? NoContent() : NotFound();
}
