using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/redirects")]
[Authorize(Roles = "Admin,Staff")]
public class AdminRedirectsController : ControllerBase
{
    private readonly RedirectService _service;
    private readonly IValidator<RedirectUpsertRequest> _validator;

    public AdminRedirectsController(RedirectService service, IValidator<RedirectUpsertRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminRedirectDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminRedirectDto>> Get(Guid id, CancellationToken ct)
    {
        var redirect = await _service.GetAsync(id, ct);
        return redirect is null ? NotFound() : Ok(redirect);
    }

    /// <summary>409 when another rule already covers the same (normalized) fromPath.</summary>
    [HttpPost]
    public async Task<ActionResult<AdminRedirectDto>> Create([FromBody] RedirectUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));

        var created = await _service.CreateAsync(request, ct);
        return created is null
            ? Conflict(new { message = "A redirect for that fromPath already exists." })
            : Ok(created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminRedirectDto>> Update(Guid id, [FromBody] RedirectUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));

        var (found, dto) = await _service.UpdateAsync(id, request, ct);
        if (!found) return NotFound();
        return dto is null
            ? Conflict(new { message = "A redirect for that fromPath already exists." })
            : Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
