# Fase 6 — Entrega (backend): usuarios por tenant desde plataforma

**Fecha:** 2026-04-30  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-6--usuarios-y-acceso-operaciones-cross-tenant-application--api).

## Resumen

- **`BlockedByPlatform`** en `ApplicationUser` (migración `AddApplicationUserBlockedByPlatform`), independiente del lockout por intentos fallidos de Identity (`LockoutEnabled` / `LockoutEnd`).
- **`PlatformAuditEventData`**: parámetro opcional **`Justification`** en la auditoría (persistencia real en Fase 10).
- **Consulta:** `IPlatformTenantUserQuery` — listado paginado de usuarios `TenantUser` por `tenantId`, filtro opcional `emailContains`.
- **Administración:** `IPlatformTenantUserAdminService` — bloquear / desbloquear por plataforma; solicitud de **reset de contraseña** y **reenvío de confirmación de email** (tokens Identity generados; sin envío SMTP en este repo — mensaje `PlatformMutationAckDto` indica conectar canal en producción).
- **API** (`[Tags("Platform")]`):
  - `GET /api/platform/tenants/{tenantId}/users` — policy **Platform.User**
  - `POST .../{userId}/block|unblock|request-password-reset|resend-email-confirmation` — body **`PlatformUserActionRequest`** (`Justification` mín. 5 caracteres), policy **Platform.Operations**
- **Login POS:** si `BlockedByPlatform`, `POST /api/auth/login` → **403** (`auth.login.platform_blocked`).
- **Tests:** `PlatformTenantUsersIntegrationTests` (listar, bloquear, login 403, desbloquear, login OK; reset ack; tenant inexistente 404).

## Notas

- No hay **invitación por email** nueva en esta entrega; el “reenvío” cubre confirmación (`GenerateEmailConfirmationTokenAsync`) para cuentas aún no confirmadas.
- La auditoría sigue siendo **NoOp** hasta implementar tabla persistente (Fase 10); la justificación ya viaja en `PlatformAuditEventData`.
