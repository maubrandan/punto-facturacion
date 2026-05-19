# Fase 2 — Entrega (backend): identidad y contexto de plataforma

**Fecha:** 2026-04-30  
Alineado con [ADR 0001](adr/0001-platform-identity-model.md), [0002](adr/0002-platform-jwt-and-authorization.md) y [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-2--dominio-identidad-y-contexto-de-plataforma-domain--application).

## Resumen

- **Domain:** `UserAccountKind`, `PlatformScope` (id sentinela y `BusinessType` placeholder), `PlatformRoleNames`, `UserAccountRules`, y `ApplicationUser.AccountKind`.
- **Application:** `ProvisionPlatformUserCommand` + `ProvisionPlatformUserResult`, `IProvisionPlatformUserHandler`, `IPlatformAuditService`, `PlatformAuditEventData`, `ProvisionPlatformUserCommandValidator` (FluentValidation).
- **Infrastructure:** `ProvisionPlatformUserHandler`, `NoOpPlatformAuditService` (hasta Fase 10), `PlatformRoleSeeder`, registro en DI, migración `AddApplicationUserAccountKind`, seed de roles `Platform.*` tras cada `Migrate`, `AuthService` rechaza login POS para `AccountKind == PlatformUser` (código `auth.login.platform_user`).
- **Sin API HTTP pública** para aprovisionar usuarios: el handler se resuelve por DI para pruebas, consola o futura Fase 4.

## Cómo aprovisionar un operador (desarrollo)

Inyectar `IProvisionPlatformUserHandler` (p. ej. test o un endpoint temporal no incluido en el repo) y llamar:

```csharp
await handler.HandleAsync(new ProvisionPlatformUserCommand(
    Email: "ops@empresa.com",
    Password: "********",
    FullName: "Operador",
    PlatformRole: PlatformRoleNames.SuperAdmin
), cancellationToken);
```

## Próximos pasos (Fase 3–4)

- Documentar acceso a datos **multi-tenant** en modo plataforma (repositorio / filtros explícitos).  
- JWT y claims `is_platform` (ADR 0002) + `api/platform/*` con policies.
