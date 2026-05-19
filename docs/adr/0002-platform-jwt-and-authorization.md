# ADR 0002: JWT, claims y autorización (policies) para plataforma

- **Estado:** Aceptada  
- **Fecha:** 2026-04-29  
- **Ámbito:** API y contratos de seguridad — Fase 1 (diseño), implementación Fase 2–4

## Contexto

El JWT actual incluye `sub`, `email`, `tenant_id` (claim personalizado), `business_type` y comparte `Issuer`/`Audience` con el POS. Los endpoints bajo `api/platform/*` deben ser atendidos solo por operadores con permisos plataforma, y **no** confundir un `tenant_id` en token de plataforma con el tenant sobre el que se está consultando (ese id va en la **ruta o query** del recurso).

## Decisión

### Claims mínimas — token **POS (tenant user)**

- Mantener las existentes alineadas con `JwtTokenService` actual.  
- Claim de tenant: tipo existente `CurrentUserService.TenantIdClaimType` (o el nombre acordado en config).

### Claims mínimas — token **plataforma**

- `sub` / `nameid` → id de usuario.  
- `email` (estándar y/o `ClaimTypes.Email`).  
- **No** emitir `tenant_id` como contexto de negocio del operador, **o** si se mantiene columna sentinela, emitir claim `platform_tenant_scope` = `"none"` o omitir; **nunca** interpretar en handlers tenant como el negocio “actual” del operador.  
- `is_platform` o `http://schemas.microsoft.com/identity/claims/scope` con valor `platform` (elegir **un** esquema y documentarlo en `JwtTokenService` / `Program.cs`).  
- `platform_role` (string) o múltiples `role` = `Platform.SuperAdmin`, etc., según mapeo Identity.  
- `jti` para revocación futura.  
- `iss` / `aud`: **misma** issuer y audience que el POS a menos que se decida en ADR 0003 separar por host; si se unifica, el **distingo** es por claim `is_platform` + `role`, no por audience distinto (alternativa: `aud: pos-clients` vs `aud: pos-platform` en otra iteración).

### Autorización ASP.NET Core

- **Policies** nombradas (no solo `[Authorize(Roles=...)]` suelto):
  - `Platform.User` — cualquier operador de plataforma (base).
  - `Platform.ReadOnly` — solo listados y export acotado (Fase 10).  
  - `Platform.Operations` — mutaciones en tenants, suspend, entitlements.  
  - `Platform.SuperAdmin` — creación de otros operadores plataforma, riesgo alto.

- Mapeo **rol Identity → policy** mediante `AddAuthorization` con `RequireRole("Platform.Operations", ...)` o con **handler** que lea `platform_role` si se prefiere un solo rol compuesto (documentar en implementación).

### ICurrentUserService (extensión conceptual)

- Para peticiones plataforma: `TenantId` = **null** o “no aplicable” para filtros de negocio; los handlers de plataforma reciben `targetTenantId` en el comando, **nunca** desde un único `TenantId` “actual” en el contexto. Documentar en interfaz o contrato de aplicación para evitar fugas (ver guía `clean-architecture-ddd`).

## Consecuencias

- **Código existente** que asume `TenantId` no nulo en cada request deberá bifurcarse: rutas `api/platform` usan otra vía (sin filtro de tenant en DbContext, o repositorio dedicado).
- **Tests:** políticas 403 en endpoints de plataforma sin claim correcta.

## Criterio de aceptación (Fase 4)

- Endpoints de ejemplo bajo `api/platform/*` con `[Authorize(Policy = "Platform.Operations")]`; JWT de usuario tenant denegado (403), JWT plataforma permitido (200).
