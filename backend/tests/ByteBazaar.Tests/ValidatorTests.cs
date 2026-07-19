using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Validators;
using ByteBazaar.Domain;
using Xunit;

namespace ByteBazaar.Tests;

public class ValidatorTests
{
    private readonly ProductUpsertRequestValidator _productValidator = new();

    [Fact]
    public void ValidProduct_Passes()
    {
        var request = new ProductUpsertRequest
        {
            Name = "Test Laptop",
            Slug = "test-laptop",
            CategoryId = Guid.NewGuid(),
            Price = 100000m,
            SalePrice = 90000m,
            Stock = 5,
            Status = ProductStatus.Active,
            Images = new List<string> { "https://placehold.co/600x400" }
        };

        var result = _productValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidSlugAndPrices_Fail()
    {
        var request = new ProductUpsertRequest
        {
            Name = "",
            Slug = "Bad Slug!",
            CategoryId = Guid.Empty,
            Price = 0m,
            SalePrice = 100m,
            Stock = -1
        };

        var result = _productValidator.Validate(request);

        Assert.False(result.IsValid);
        var properties = result.Errors.Select(e => e.PropertyName).ToHashSet();
        Assert.Contains(nameof(ProductUpsertRequest.Name), properties);
        Assert.Contains(nameof(ProductUpsertRequest.Slug), properties);
        Assert.Contains(nameof(ProductUpsertRequest.CategoryId), properties);
        Assert.Contains(nameof(ProductUpsertRequest.Price), properties);
        Assert.Contains(nameof(ProductUpsertRequest.Stock), properties);
    }

    [Fact]
    public void SalePriceNotBelowPrice_Fails()
    {
        var request = new ProductUpsertRequest
        {
            Name = "Test",
            Slug = "test",
            CategoryId = Guid.NewGuid(),
            Price = 100m,
            SalePrice = 100m,
            Stock = 0
        };

        var result = _productValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ProductUpsertRequest.SalePrice));
    }

    [Fact]
    public void AttributeCode_MustBeSnakeCase()
    {
        var validator = new AttributeUpsertRequestValidator();
        var request = new AttributeUpsertRequest
        {
            CategoryId = Guid.NewGuid(),
            Name = "Screen Size",
            Code = "Screen Size"
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AttributeUpsertRequest.Code));

        request.Code = "screen_size";
        Assert.True(validator.Validate(request).IsValid);
    }
}
