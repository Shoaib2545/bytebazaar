using ByteBazaar.Application.DTOs;
using ByteBazaar.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/customers")]
[Authorize(Roles = "Admin,Staff")]
public class AdminCustomersController : ControllerBase
{
    private readonly AdminCustomerService _service;

    public AdminCustomersController(AdminCustomerService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<AdminCustomerListItemDto>>> GetAll(
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetCustomersAsync(search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminCustomerDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var customer = await _service.GetCustomerAsync(id, ct);
        return customer is null ? NotFound() : Ok(customer);
    }
}
