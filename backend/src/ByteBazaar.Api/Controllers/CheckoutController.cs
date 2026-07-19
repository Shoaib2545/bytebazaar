using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers;

[ApiController]
[Route("api/checkout")]
public class CheckoutController : ControllerBase
{
    private readonly CheckoutService _checkoutService;

    public CheckoutController(CheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    [HttpGet("shipping-options")]
    public ActionResult<IReadOnlyList<ShippingOptionDto>> GetShippingOptions()
        => Ok(_checkoutService.GetShippingOptions());

    [HttpPost]
    public async Task<ActionResult<CheckoutResultDto>> Checkout(
        [FromBody] CheckoutRequest request, [FromServices] IValidator<CheckoutRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));

        var userId = User.GetUserId();
        Guid? anonymousId = null;
        if (userId is null &&
            Request.Cookies.TryGetValue(CartController.CartCookieName, out var raw) &&
            Guid.TryParse(raw, out var parsed))
        {
            anonymousId = parsed;
        }

        return Ok(await _checkoutService.CheckoutAsync(userId, anonymousId, request, ct));
    }
}
