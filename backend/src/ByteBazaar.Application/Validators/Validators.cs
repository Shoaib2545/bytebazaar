using ByteBazaar.Application.DTOs;
using FluentValidation;

namespace ByteBazaar.Application.Validators;

public static class ValidationRules
{
    public const string SlugPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
    public const string CodePattern = "^[a-z0-9_]+$";
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class CategoryUpsertRequestValidator : AbstractValidator<CategoryUpsertRequest>
{
    public CategoryUpsertRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200).Matches(ValidationRules.SlugPattern)
            .WithMessage("Slug must contain only lowercase letters, digits and hyphens.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MetaTitle).MaximumLength(200);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
    }
}

public class AttributeUpsertRequestValidator : AbstractValidator<AttributeUpsertRequest>
{
    public AttributeUpsertRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100).Matches(ValidationRules.CodePattern)
            .WithMessage("Code must contain only lowercase letters, digits and underscores.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.FilterWidget).IsInEnum();
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Options).NotEmpty().MaximumLength(200);
    }
}

public class BrandUpsertRequestValidator : AbstractValidator<BrandUpsertRequest>
{
    public BrandUpsertRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200).Matches(ValidationRules.SlugPattern)
            .WithMessage("Slug must contain only lowercase letters, digits and hyphens.");
        RuleFor(x => x.LogoUrl).MaximumLength(500);
    }
}

public class ProductUpsertRequestValidator : AbstractValidator<ProductUpsertRequest>
{
    public ProductUpsertRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(300).Matches(ValidationRules.SlugPattern)
            .WithMessage("Slug must contain only lowercase letters, digits and hyphens.");
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.SalePrice).GreaterThan(0).When(x => x.SalePrice is not null);
        RuleFor(x => x.SalePrice).LessThan(x => x.Price).When(x => x.SalePrice is not null)
            .WithMessage("Sale price must be lower than the regular price.");
        RuleFor(x => x.SaleEnd).GreaterThan(x => x.SaleStart!.Value)
            .When(x => x.SaleStart is not null && x.SaleEnd is not null)
            .WithMessage("Sale end must be after sale start.");
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
        RuleForEach(x => x.Images).NotEmpty().MaximumLength(500);
        RuleFor(x => x.MetaTitle).MaximumLength(200);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
    }
}
