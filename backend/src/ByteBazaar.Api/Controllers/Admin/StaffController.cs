using ByteBazaar.Application.DTOs;
using ByteBazaar.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

/// <summary>Staff account management — Admin role ONLY (Staff gets 403).</summary>
[ApiController]
[Route("api/admin/staff")]
[Authorize(Roles = "Admin")]
public class StaffController : ControllerBase
{
    private readonly StaffService _service;

    public StaffController(StaffService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<StaffUserDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetStaffAsync(ct));

    [HttpPost]
    public async Task<ActionResult<StaffUserDto>> Create(
        [FromBody] StaffCreateRequest request, [FromServices] IValidator<StaffCreateRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        return Ok(await _service.CreateAsync(request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffUserDto>> Update(
        Guid id, [FromBody] StaffUpdateRequest request, [FromServices] IValidator<StaffUpdateRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        var updated = await _service.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id, [FromBody] ResetPasswordRequest request, [FromServices] IValidator<ResetPasswordRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));
        return await _service.ResetPasswordAsync(id, request.NewPassword, ct) ? NoContent() : NotFound();
    }
}
