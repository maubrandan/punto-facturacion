using FluentValidation;
using POS.Application.Platform;

namespace POS.Application.Platform.Validation;

public sealed class UnsuspendPlatformTenantCommandValidator : AbstractValidator<UnsuspendPlatformTenantCommand>
{
    public UnsuspendPlatformTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().MaximumLength(128);
    }
}
