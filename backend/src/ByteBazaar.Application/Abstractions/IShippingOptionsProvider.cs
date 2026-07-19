using ByteBazaar.Application.DTOs;

namespace ByteBazaar.Application.Abstractions;

public interface IShippingOptionsProvider
{
    IReadOnlyList<ShippingOptionDto> GetOptions();
}
