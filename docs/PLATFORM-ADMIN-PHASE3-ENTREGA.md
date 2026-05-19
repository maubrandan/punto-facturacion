# Fase 3 — Entrega (backend): persistencia, Identity y filtros seguros

**Fecha:** 2026-04-30  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-3--infraestructura-persistencia-identity-y-filtros-seguros-infrastructure), [PLATFORM-QUERY-FILTERS](PLATFORM-QUERY-FILTERS.md) y ADRs 0001–0002.

## Resumen

- **Consulta directorio de tenants:** `IPlatformDirectoryQuery` + `PlatformDirectoryQuery`, DTO `TenantSummaryDto`.
- **Helpers EF plataforma:** `PlatformEfQueryExtensions.FilterByTenant<T>()` (`IgnoreQueryFilters` + `Where(TenantId == …)` para `ITenantEntity`).
- **Claims:** `PlatformClaimTypes.IsPlatform`; `ICurrentUserService.IsPlatformContext` (JWT en Fase 4).
- **Seed opcional:** `PlatformAdminSeedOptions` + bloque en `DbInitializer` vía `IProvisionPlatformUserHandler` (credenciales solo en configuración; deshabilitado por defecto en Development).
- **`DbInitializer`:** migraciones y roles `Platform.*` siempre; seeds de negocio y plataforma opcionales por configuración.
- **`SaveChanges`:** el tenant de contexto solo es obligatorio al persistir **`ITenantEntity` nuevas sin `TenantId`** (permite seed de `IdentityRole` y tablas sin tenant en arranque).
- **Documentación:** [PLATFORM-QUERY-FILTERS.md](PLATFORM-QUERY-FILTERS.md) (filtros, excepciones, antipatrones).
- **Tests:** `PlatformDirectoryQueryTests` + `TestWebApplicationFactory` con `PlatformAdminSeed:Enabled=false`.

## Próximos pasos (Fase 4)

- JWT de consola plataforma, `api/platform/*`, policies y uso real de `IsPlatformContext`.
