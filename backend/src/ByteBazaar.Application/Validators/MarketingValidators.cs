using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain;
using FluentValidation;

namespace ByteBazaar.Application.Validators;

public class ApplyCouponRequestValidator : AbstractValidator<ApplyCouponRequest>
{
    public ApplyCouponRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}

public class CouponUpsertRequestValidator : AbstractValidator<CouponUpsertRequest>
{
    public CouponUpsertRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Code may contain only letters, digits, hyphens and underscores.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => x.Type == CouponType.Percent)
            .WithMessage("Percent coupons cannot exceed 100.");
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinOrderAmount is not null);
        RuleFor(x => x.MaxUses).GreaterThan(0).When(x => x.MaxUses is not null);
        RuleFor(x => x.ValidTo).GreaterThan(x => x.ValidFrom!.Value)
            .When(x => x.ValidFrom is not null && x.ValidTo is not null)
            .WithMessage("validTo must be after validFrom.");
    }
}

public class BannerUpsertRequestValidator : AbstractValidator<BannerUpsertRequest>
{
    public BannerUpsertRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subtitle).MaximumLength(500);
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500);
        RuleFor(x => x.LinkUrl).MaximumLength(500);
        RuleFor(x => x.Placement).IsInEnum();
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt!.Value)
            .When(x => x.StartsAt is not null && x.EndsAt is not null)
            .WithMessage("endsAt must be after startsAt.");
    }
}

public class StaffCreateRequestValidator : AbstractValidator<StaffCreateRequest>
{
    public StaffCreateRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Role).Must(r => r is "Admin" or "Staff")
            .WithMessage("Role must be \"Admin\" or \"Staff\".");
    }
}

public class StaffUpdateRequestValidator : AbstractValidator<StaffUpdateRequest>
{
    public StaffUpdateRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role).Must(r => r is "Admin" or "Staff")
            .WithMessage("Role must be \"Admin\" or \"Staff\".");
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}
