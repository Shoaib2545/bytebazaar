using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = "Admin,Staff")]
public class AdminCategoriesController : ControllerBase
{
    private readonly AdminCatalogService _service;
    private readonly IValidator<CategoryUpsertRequest> _validator;

    public AdminCategoriesController(AdminCatalogService service, IValidator<CategoryUpsertRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminCategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetCategoriesAsync(ct));

    [HttpPost]
    public async Task<ActionResult<AdminCategoryDto>> Create([FromBody] CategoryUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        var created = await _service.CreateCategoryAsync(request, ct);
        return Ok(created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminCategoryDto>> Update(Guid id, [FromBody] CategoryUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        var updated = await _service.UpdateCategoryAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _service.DeleteCategoryAsync(id, ct) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/attributes")]
    public async Task<ActionResult<List<AdminAttributeDto>>> GetAttributes(
        Guid id, [FromServices] AdminCatalogService service, CancellationToken ct)
    {
        var attributes = await service.GetCategoryAttributesAsync(id, ct);
        return attributes is null ? NotFound() : Ok(attributes);
    }
}
