# Fase 4 — Entrega (backend): API plataforma `api/platform/*`

**Fecha:** 2026-04-30  
Alineado con [ADR 0002](adr/0002-platform-jwt-and-authorization.md) y [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-4--api-superficie-aislada-apiproplatform-api).

## Resumen

- **JWT plataforma:** `IJwtTokenService.CreatePlatformToken` — claims `is_platform=true`, roles `Platform.*` como `ClaimTypes.Role`, claim opcional `platform_role`; **sin** `tenant_id` (no usar como tenant de negocio).
- **Login:** `POST /api/platform/auth/login` — solo `AccountKind == PlatformUser` con al menos un rol `Platform.*`; respuesta `AuthResponse` con `TenantId` vacío y `BusinessType = Platform`.
- **Login POS** (`POST /api/auth/login`) sigue rechazando usuarios plataforma con código `auth.login.platform_user` y mensaje que indica el endpoint de consola.
- **Policies:** `Platform.User`, `Platform.ReadOnly`, `Platform.Operations`, `Platform.SuperAdmin` (`AuthorizationPolicies` en Application).
- **Endpoints:**  
  - `GET /api/platform/health` · `GET /api/platform/version` — anónimos, `ApiResponse<T>`.  
  - `GET /api/platform/tenants` — paginado (`page`, `pageSize` ≤ 100), policy `Platform.User`, `TenantDirectoryPageDto`.  
- **Validación JWT:** `NameClaimType` / `RoleClaimType` alineados con `ClaimTypes` para `IsInRole` en policies.
- **Correlación:** middleware que asegura cabecera de respuesta `X-Request-Id` (y genera id si no viene en la petición).
- **OpenAPI / Swagger:** atributo `[Tags("Platform")]` en controladores (grupo en documentación OpenAPI de desarrollo).
- **Tests de integración:** 401 sin identidad, 403 usuario tenant, 200 con claims de plataforma; health anónimo; presencia de `X-Request-Id`.

## Próximos pasos (Fase 5+)

- CRUD y estados de `Tenant`, listados con más filtros, y endpoints con `Platform.Operations` según la matriz de ADR.
