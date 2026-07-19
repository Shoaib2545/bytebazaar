using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/banners")]
[Authorize(Roles = "Admin,Staff")]
public class AdminBannersController : ControllerBase
{
    private readonly BannerService _service;
    private readonly IValidator<BannerUpsertRequest> _validator;

    public AdminBannersController(BannerService service, IValidator<BannerUpsertRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminBannerDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetBannersAsync(ct));

    [HttpPost]
    public async Task<ActionResult<AdminBannerDto>> Create([FromBody] BannerUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        return Ok(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminBannerDto>> Update(Guid id, [FromBody] BannerUpsertRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        var updated = await _service.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
