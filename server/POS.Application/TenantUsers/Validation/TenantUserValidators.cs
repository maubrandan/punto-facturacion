using FluentValidation;
using POS.Application.TenantUsers;
using POS.Domain.Tenant;

namespace POS.Application.TenantUsers.Validation;

public sealed class CreateTenantUserCommandValidator : AbstractValidator<CreateTenantUserCommand>
{
    public CreateTenantUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(128);
        RuleFor(x => x.FullName).MaximumLength(512);
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(TenantRoleNames.IsKnownRole)
            .WithMessage("El rol debe ser Tenant.Admin, Tenant.Cashier o Tenant.Stock.");
    }
}

public sealed class UpdateTenantUserCommandValidator : AbstractValidator<UpdateTenantUserCommand>
{
    public UpdateTenantUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(TenantRoleNames.IsKnownRole)
            .WithMessage("El rol debe ser Tenant.Admin, Tenant.Cashier o Tenant.Stock.");
    }
}
