# Estrategia de query filters y acceso plataforma (EF Core)

Este documento cierra el entregable de **Fase 3** del plan de admin de plataforma: evitar fugas cross-tenant y centralizar el patrón de consulta.

## Qué está filtrado en `ApplicationDbContext`

- Cualquier `ITenantEntity` recibe en `OnModelCreating` un filtro global:  
  `e => e.TenantId == _currentTenantId` (campo de instancia del `DbContext`, sincronizado con `ICurrentUserService` antes de `SaveChanges` y al construir el contexto).
- **Excepciones explícitas (sin filtro global en esta capa):**
  - `ApplicationUser` — búsqueda por email en login/registro.
  - `Product` — el listado/venta debe acotar **siempre** por `TenantId` en el handler o en la API (el filtro no se aplica a nivel EF para este tipo).

## Entidades “sin” tenant

- `Tenant` (catálogo de comercios) no implementa `ITenantEntity` y no lleva filtro; es la tabla que un operador de plataforma lee con `IPlatformDirectoryQuery` / `DbSet<Tenants>`.

## Modo plataforma (operador global)

1. **Lectura cross-tenant**  
   - **Tenants:** `GET /api/platform/tenants` (policy `Platform.User`) o `IPlatformDirectoryQuery.ListTenantsPageAsync` / `ListAllTenantsAsync` (sin `IgnoreQueryFilters`).  
   - **Entidades con `ITenantEntity` + filtro:** usar **solo** el helper `PlatformEfQueryExtensions.FilterByTenant` (en `POS.Infrastructure.Platform`) o una consulta equivalente con `IgnoreQueryFilters()` + `Where(e => e.TenantId == idObjetivo)` con `idObjetivo` **validado** (tenant existe, estado, permisos vía Fase 4+).

2. **Escritura**  
   - No confiar en el filtro global para el “tenant actual” del operador.  
   - Fijar `ICurrentUserTenantContext.OverriddenTenantId` al **tenant objetivo** del cambio, o asignar `TenantId` en entidades agregadas de forma explícita.  
   - `SaveChanges` exige `ICurrentUserService.TenantId` (o `TenantId` ya fijado en la entidad) **solo** al insertar `ITenantEntity` sin `TenantId`; operaciones que solo tocan tablas sin tenant (p. ej. `IdentityRole` en el arranque) o `Tenant` no requieren contexto de tenant.

3. **Claim `is_platform`**  
   - `ICurrentUserService.IsPlatformContext` cuando el JWT incluye `is_platform=true` (p. ej. tras `POST /api/platform/auth/login`).

4. **Productos y ventas**  
   - Cualquier carga de `Product` por id debe incluir `p.TenantId == tenantIdOperacion` (ej. `CreateSaleHandler`).

## Estado del negocio (`Tenant.Status`)

- Si `Tenant.Status` es **Suspended** o **Closed**, el login POS (`POST /api/auth/login`) falla con **403** y código `auth.login.tenant_suspended` / `auth.login.tenant_closed` (no aplica a JWT de plataforma).

## Bloqueo por plataforma (`ApplicationUser.BlockedByPlatform`)

- Independiente del lockout por intentos de Identity. Si está activo, login POS falla con **403** (`auth.login.platform_blocked`). Solo operadores de plataforma pueden bloquear/desbloquear vía API (`api/platform/tenants/{tenantId}/users/...`).

## Antipatrones

- `IgnoreQueryFilters()` sin `Where` por `TenantId` en entidades de negocio.  
- Asumir que el JWT de un cajero puede listar datos de otro tenant sin comprobación.  
- Usar el id sentinela de plataforma (`PlatformScope.ReservedTenantId`) como tenant de **negocio** en reglas de catálogo o venta.

## Referencias

- [ADR 0001](adr/0001-platform-identity-model.md), [0002](adr/0002-platform-jwt-and-authorization.md)  
- [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-3--infraestructura-persistencia-identity-y-filtros-seguros-infrastructure)
