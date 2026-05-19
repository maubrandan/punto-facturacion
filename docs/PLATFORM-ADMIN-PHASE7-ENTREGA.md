# Fase 7 — Entrega (backend): suplantación controlada

**Fecha:** 2026-04-29  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-7--ver-como--suplantación-controlada-opcional-pero-típica-en-plataformas).

## Resumen

- **Claims JWT:** `impersonation`, `imp_reason`; sin `is_platform` en el token de sesión tenant (TTL 1–60 min).
- **`IJwtTokenService.CreateImpersonationToken`:** operador `PlatformUser`, `tenant_id` = negocio objetivo, motivo acotado.
- **`StartImpersonationSessionCommand`** + validador; **`ImpersonationSessionService`:** tenant existente y **Active**; auditoría `ImpersonationSessionStarted` con justificación.
- **Policy** `Platform.Impersonation` (`Support`, `SupportReadOnly`, `Operations`, `SuperAdmin`).
- **API:** `POST /api/platform/support/impersonation/session` — body `tenantId`, `reason`, `ttlMinutes` (por defecto 15 en `StartImpersonationSessionApiRequest`).
- **Tests:**
  - TestAuth: `Tenants_WithImpersonationClaims_Returns403` — JWT simulado de suplantación no entra a rutas `api/platform/*`.
  - JWT real (`JwtBearerIntegrationTestFactory`): login plataforma → POST sesión → claims esperados → `GET /api/platform/tenants` → **403**.

## Notas

- El factory JWT usa **`IWebHostBuilder.UseSetting`** para `Jwt:*` y cadena SQL, de modo que la clave coincida con **`IOptions<JwtOptions>`** y con la configuración de JwtBearer en `Program.cs` (los valores en `appsettings.json` del API tienen prioridad sobre `AddInMemoryCollection` si no se fuerzan así).
- Banner en POS (Fase 12) no forma parte de esta entrega.
