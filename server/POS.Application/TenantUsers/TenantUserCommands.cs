namespace POS.Application.TenantUsers;

public sealed record CreateTenantUserCommand(
    string Email,
    string Password,
    string FullName,
    string Role);

public sealed record UpdateTenantUserCommand(
    string UserId,
    string FullName,
    string Role);

public sealed record SetTenantUserBlockedCommand(
    string UserId,
    bool Blocked);
