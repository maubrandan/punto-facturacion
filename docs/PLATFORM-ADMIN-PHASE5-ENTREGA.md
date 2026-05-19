# Fase 5 — Entrega (backend): ciclo de vida de tenants

**Fecha:** 2026-04-30  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-5--gestión-de-tenants-ciclo-de-vida-completo-application--api).

## Resumen

- **Domain:** `TenantStatus` (`Active`, `Suspended`, `Closed`), campos en `Tenant` (`ContactEmail`, `Status`, `UpdatedAt`, `SuspendedAt`, `ClosedAt`), reglas estáticas en `TenantLifecycleRules`.
- **Migración EF:** `AddTenantLifecycle` (`Status` por defecto `0` = Active para filas existentes).
- **Application:** comandos `CreatePlatformTenantCommand`, `UpdatePlatformTenantCommand`, `SuspendPlatformTenantCommand`, `ClosePlatformTenantCommand`; `IPlatformTenantLifecycleService`; DTOs `TenantDetailDto`, `TenantListFilter`; validadores FluentValidation.
- **Infrastructure:** `PlatformTenantLifecycleService`, consultas ampliadas en `PlatformDirectoryQuery` (filtros por nombre, estado, rango `CreatedAt`).
- **API (`api/platform/tenants`):**
  - Lectura: `GET` paginado con query (`nameContains`, `status`, `createdFromUtc`, `createdToUtc`), `GET {id}` — policy **Platform.User**.
  - Escritura: `POST` crear, `PATCH {id}` actualizar, `POST {id}/suspend`, `POST {id}/close` — policy **Platform.Operations**.
- **Login POS:** si el tenant está `Suspended` o `Closed`, `POST /api/auth/login` devuelve **403** con códigos `auth.login.tenant_suspended` / `auth.login.tenant_closed`.
- **Pruebas:** `PlatformTenantLifecycleIntegrationTests` (crear, listar/filtrar, suspender idempotente, login bloqueado).

## Notas

- **Cerrar** (`Closed`) es idempotente; **suspender** también si ya está suspendido.
- Evento de dominio `TenantCreated` no se implementó (opcional en el plan).
- Reactivar un tenant suspendido no está en esta entrega (puede añadirse como comando explícito más adelante).
