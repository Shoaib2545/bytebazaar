using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using FluentValidation;

namespace ByteBazaar.Application.Validators;

public class RedirectUpsertRequestValidator : AbstractValidator<RedirectUpsertRequest>
{
    public RedirectUpsertRequestValidator()
    {
        RuleFor(x => x.FromPath).NotEmpty().MaximumLength(500)
            .Must(p => p.Trim().StartsWith('/'))
            .WithMessage("fromPath must be a site-relative path starting with \"/\".");

        RuleFor(x => x.ToPath).NotEmpty().MaximumLength(500)
            .Must(p => p.Trim().StartsWith('/')
                       || p.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                       || p.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .WithMessage("toPath must start with \"/\" or be an absolute http(s) URL.");

        RuleFor(x => x)
            .Must(x => RedirectService.NormalizePath(x.FromPath) != RedirectService.NormalizePath(x.ToPath))
            .WithMessage("fromPath and toPath must differ (a redirect to itself loops).")
            .When(x => !string.IsNullOrWhiteSpace(x.FromPath) && x.ToPath.Trim().StartsWith('/'));
    }
}
