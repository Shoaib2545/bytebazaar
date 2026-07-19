using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize]
public class AddressesController : ControllerBase
{
    private readonly AddressService _addressService;
    private readonly IValidator<AddressUpsertRequest> _validator;

    public AddressesController(AddressService addressService, IValidator<AddressUpsertRequest> validator)
    {
        _addressService = addressService;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<List<AddressDto>>> GetAll(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _addressService.GetAddressesAsync(userId.Value, ct));
    }

    [HttpPost]
    public async Task<ActionResult<AddressDto>> Create([FromBody] AddressUpsertRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));

        return Ok(await _addressService.CreateAsync(userId.Value, request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AddressDto>> Update(Guid id, [FromBody] AddressUpsertRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));

        var updated = await _addressService.UpdateAsync(userId.Value, id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        return await _addressService.DeleteAsync(userId.Value, id, ct) ? NoContent() : NotFound();
    }
}
