using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin,Staff")]
public class AdminOrdersController : ControllerBase
{
    private readonly AdminOrderService _service;
    private readonly IValidator<OrderStatusUpdateRequest> _statusValidator;

    public AdminOrdersController(AdminOrderService service, IValidator<OrderStatusUpdateRequest> statusValidator)
    {
        _service = service;
        _statusValidator = statusValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<AdminOrderListItemDto>>> GetAll(
        [FromQuery] string? status = null, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetOrdersAsync(status, search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminOrderDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var order = await _service.GetOrderAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<AdminOrderDetailDto>> UpdateStatus(
        Guid id, [FromBody] OrderStatusUpdateRequest request, CancellationToken ct)
    {
        var validation = await _statusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));

        var updated = await _service.TransitionStatusAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
