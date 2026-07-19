using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/attributes")]
[Authorize(Roles = "Admin,Staff")]
public class AdminAttributesController : ControllerBase
{
    private readonly AdminCatalogService _service;
    private readonly IValidator<AttributeUpsertRequest> _validator;

    public AdminAttributesController(AdminCatalogService service, IValidator<AttributeUpsertRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost]
    public async Task<ActionResult<AdminAttributeDto>> Create([FromBody] AttributeUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        var created = await _service.CreateAttributeAsync(request, ct);
        return created is null
            ? NotFound(new ProblemDetails { Status = 404, Title = "Category not found" })
            : Ok(created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminAttributeDto>> Update(Guid id, [FromBody] AttributeUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        var updated = await _service.UpdateAttributeAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _service.DeleteAttributeAsync(id, ct) ? NoContent() : NotFound();
}
