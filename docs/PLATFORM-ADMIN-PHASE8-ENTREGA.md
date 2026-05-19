# Fase 8 — Entrega (backend): cumplimiento Auth para tenant suspendido/cerrado

**Fecha:** 2026-04-30  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-8--cumplimiento-de-auth-para-tenant-suspendidocerrado).

## Resumen

- `AuthService.LoginAsync` valida estado del tenant para cuentas `TenantUser`:
  - `Suspended` -> falla con `auth.login.tenant_suspended`.
  - `Closed` -> falla con `auth.login.tenant_closed`.
- La respuesta mantiene el contrato uniforme `ApiResponse` y se traduce a HTTP **403** en `POST /api/auth/login`.

## Tests de integración

- `PlatformTenantLifecycleIntegrationTests.Login_Returns403_WhenTenantSuspended`.
- `PlatformTenantLifecycleIntegrationTests.Login_Returns403_WhenTenantClosed`.

## Nota

- El roadmap menciona también refresh tokens; actualmente este backend no expone endpoint de refresh, por lo que el alcance real de Fase 8 en este repositorio aplica al flujo de login.
