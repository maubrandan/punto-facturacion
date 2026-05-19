# Fase 10 — Entrega (backend): auditoría inmutable de plataforma

**Fecha:** 2026-04-30  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-10--auditoría-inmutable-append-only-y-export).

## Resumen

- **Persistencia real** de auditoría (se reemplaza `NoOp`):
  - Entidad `PlatformAuditEvent` (tabla `PlatformAuditEvents`, append-only por diseño de aplicación).
  - Campos: actor (`ActorUserId`, `ActorEmail`), acción, recurso, `AffectedTenantId`, detalles, justificación, `CorrelationId`, `IpAddress`, `CreatedAtUtc`, `IsImpersonationContext`.
- **Servicio** `EfPlatformAuditService`:
  - Registra cada evento en DB.
  - Toma correlación desde `X-Request-Id` y actor desde claims del request.
- **Query API**:
  - `GET /api/platform/audit` (`Platform.User`) con paginación y filtros por `tenantId`, `actorUserId`, rango `createdFromUtc/createdToUtc`.
  - Contratos: `PlatformAuditEventDto`, `PlatformAuditEventPageDto`, `PlatformAuditListFilter`.
- **Índices** para filtros frecuentes: `CreatedAtUtc`, `AffectedTenantId`, `ActorUserId`.

## Cambios de modelo y migración

- Nueva entidad de dominio: `PlatformAuditEvent`.
- `ApplicationDbContext`: `DbSet<PlatformAuditEvent>` + configuración fluida.
- Migración EF: `AddPlatformAuditEvents`.

## Cobertura

- `PlatformAuditIntegrationTests`:
  - Ejecuta una acción de plataforma (`TenantEntitlementsUpdated`) y verifica que aparece en `/api/platform/audit`.
  - Verifica filtro por `tenantId`.
- `PlatformApiAuthorizationTests`:
  - Usuario tenant (`X-Test-TenantId`) recibe **403** al consultar `/api/platform/audit`.

## Nota

- La parte de **export CSV/JSON con rate limit** queda como siguiente iteración de Fase 10 (API de consulta paginada ya disponible).
