using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    internal const string CartCookieName = "bb_cart_id";

    private readonly CartService _cartService;
    private readonly CouponService _couponService;

    public CartController(CartService cartService, CouponService couponService)
    {
        _cartService = cartService;
        _couponService = couponService;
    }

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct)
    {
        var (userId, anonymousId) = ResolveIdentity();
        return Ok(await _cartService.GetCartAsync(userId, anonymousId, ct));
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(
        [FromBody] AddCartItemRequest request, [FromServices] IValidator<AddCartItemRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));

        var (userId, anonymousId) = ResolveIdentity();
        return Ok(await _cartService.AddItemAsync(userId, anonymousId, request, ct));
    }

    [HttpPut("items/{productId:guid}")]
    public async Task<ActionResult<CartDto>> UpdateItem(
        Guid productId, [FromBody] UpdateCartItemRequest request,
        [FromServices] IValidator<UpdateCartItemRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));

        var (userId, anonymousId) = ResolveIdentity();
        return Ok(await _cartService.UpdateItemAsync(userId, anonymousId, productId, request.Quantity, ct));
    }

    [HttpDelete("items/{productId:guid}")]
    public async Task<ActionResult<CartDto>> RemoveItem(Guid productId, CancellationToken ct)
    {
        var (userId, anonymousId) = ResolveIdentity();
        return Ok(await _cartService.RemoveItemAsync(userId, anonymousId, productId, ct));
    }

    [HttpPost("coupon")]
    public async Task<ActionResult<CartDto>> ApplyCoupon(
        [FromBody] ApplyCouponRequest request, [FromServices] IValidator<ApplyCouponRequest> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation.ToModelState(ModelState));

        var (userId, anonymousId) = ResolveIdentity();
        return Ok(await _couponService.ApplyAsync(userId, anonymousId, request.Code, ct));
    }

    [HttpDelete("coupon")]
    public async Task<ActionResult<CartDto>> RemoveCoupon(CancellationToken ct)
    {
        var (userId, anonymousId) = ResolveIdentity();
        return Ok(await _couponService.RemoveAsync(userId, anonymousId, ct));
    }

    /// <summary>Merges the anonymous cookie cart into the authenticated user's cart (call after login).</summary>
    [HttpPost("merge")]
    [Authorize]
    public async Task<ActionResult<CartDto>> Merge(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        Guid? anonymousId = Request.Cookies.TryGetValue(CartCookieName, out var raw) && Guid.TryParse(raw, out var parsed)
            ? parsed
            : null;

        var cart = await _cartService.MergeAsync(userId.Value, anonymousId, ct);
        if (anonymousId is not null)
            Response.Cookies.Delete(CartCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        return Ok(cart);
    }

    private (Guid? UserId, Guid? AnonymousId) ResolveIdentity()
    {
        var userId = User.GetUserId();
        if (userId is not null) return (userId, null);
        return (null, GetOrIssueAnonymousId());
    }

    private Guid GetOrIssueAnonymousId()
    {
        if (Request.Cookies.TryGetValue(CartCookieName, out var raw) && Guid.TryParse(raw, out var existing))
            return existing;

        var id = Guid.NewGuid();
        Response.Cookies.Append(CartCookieName, id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });
        return id;
    }
}
