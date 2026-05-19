# Fase 11 — Entrega (frontend v1): consola de plataforma

**Fecha:** 2026-04-30  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-11--front-de-plataforma-aplicación-o-módulo-dedicado-angular).

## Resumen

- Se agregó una **consola de plataforma** separada del shell tenant dentro de `pos-frontend`:
  - Login: `/platform/login`
  - Shell dedicado: `/platform/*`
  - Vistas iniciales:
    - `/platform/dashboard` (home; KPIs desde API de métricas, ver [PLATFORM-ADMIN-PHASE13-ENTREGA](PLATFORM-ADMIN-PHASE13-ENTREGA.md))
    - `/platform/tenants`
    - `/platform/audit`
- Se incorporó guard específico `platformAuthGuard` y autenticación dedicada `PlatformAuthService`.
- Se usa token independiente en frontend: `platform_auth_token`.
- El interceptor HTTP ahora enruta credenciales por prefijo:
  - `/api/platform/*` -> token plataforma
  - resto `/api/*` -> token tenant (`auth_token`)
- Se completó el siguiente paso natural de v1 en detalle tenant:
  - bloque de **usuarios del tenant** (listar, filtrar por email, bloquear/desbloquear, request password reset y resend email confirmation)
  - justificación operativa requerida en UI para mutaciones

## Detalle técnico

- Nuevos archivos principales:
  - `core/services/platform-auth.service.ts`
  - `core/guards/platform-auth.guard.ts`
  - `core/services/platform-console.service.ts`
  - `features/platform/layout/platform-shell.component.ts`
  - `features/platform/pages/platform-login.component.ts`
  - `features/platform/pages/platform-tenants-page.component.ts`
  - `features/platform/pages/platform-audit-page.component.ts`
  - `features/platform/pages/platform-dashboard-page.component.ts`
- `app.routes.ts` ahora define las rutas `/platform` en un árbol propio.
- Extensión en archivos existentes:
  - `features/platform/pages/platform-tenant-detail-page.component.ts` ahora incluye el bloque operativo de usuarios tenant.
  - `core/services/platform-console.service.ts` expone contratos/métodos para `/api/platform/tenants/{tenantId}/users/*`.

## Verificación

- `npm run build` en `pos-frontend` correcto.
- Sin errores de linter en archivos nuevos/modificados.

## Nota

- Esta entrega es **v1**: prioriza separación de flujo y pantallas mínimas.
- Pendientes naturales para siguiente iteración: métricas de producto avanzadas (DAU / ventas 24 h, etc. en Fase 13/14), export/auditoría avanzada y UX refinada.
