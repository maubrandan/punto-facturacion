using FluentValidation;
using POS.Application.Platform;

namespace POS.Application.Platform.Validation;

public sealed class ClosePlatformTenantCommandValidator : AbstractValidator<ClosePlatformTenantCommand>
{
    public ClosePlatformTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().MaximumLength(128);
    }
}
