using FluentValidation;
using POS.Application.Platform;

namespace POS.Application.Platform.Validation;

public sealed class SuspendPlatformTenantCommandValidator : AbstractValidator<SuspendPlatformTenantCommand>
{
    public SuspendPlatformTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().MaximumLength(128);
    }
}
