using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;

namespace ByteBazaar.Application.Services;

public class DefaultShippingOptionsProvider : IShippingOptionsProvider
{
    private static readonly List<ShippingOptionDto> Options = new()
    {
        new ShippingOptionDto { Code = "standard", Name = "Standard Delivery (3-5 days)", Fee = 250m },
        new ShippingOptionDto { Code = "express", Name = "Express Delivery (1-2 days)", Fee = 600m }
    };

    public IReadOnlyList<ShippingOptionDto> GetOptions() => Options;
}
