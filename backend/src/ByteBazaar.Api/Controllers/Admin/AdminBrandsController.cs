using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/brands")]
[Authorize(Roles = "Admin,Staff")]
public class AdminBrandsController : ControllerBase
{
    private readonly AdminCatalogService _service;
    private readonly IValidator<BrandUpsertRequest> _validator;

    public AdminBrandsController(AdminCatalogService service, IValidator<BrandUpsertRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminBrandDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetBrandsAsync(ct));

    [HttpPost]
    public async Task<ActionResult<AdminBrandDto>> Create([FromBody] BrandUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        return Ok(await _service.CreateBrandAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminBrandDto>> Update(Guid id, [FromBody] BrandUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        var updated = await _service.UpdateBrandAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _service.DeleteBrandAsync(id, ct) ? NoContent() : NotFound();
}
