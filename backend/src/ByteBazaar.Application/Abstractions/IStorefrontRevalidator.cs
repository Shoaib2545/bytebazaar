namespace ByteBazaar.Application.Abstractions;

/// <summary>
/// Fire-and-forget storefront ISR revalidation. Implementations must never throw —
/// failures are logged as warnings only.
/// </summary>
public interface IStorefrontRevalidator
{
    void Revalidate(params string[] paths);
}
