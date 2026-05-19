# Fase 12 — Entrega (front tenant — transparencia)

**Fecha:** 2026-04-30  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-12--ajustes-en-front-tenant-transparencia).

## Backend (referencia)

El login POS (`POST /api/auth/login`) ya distingue negocio no operativo según ciclo de vida del tenant en `server/POS.Infrastructure/Services/AuthService.cs`:

| Código en envelope (`error.code`) | HTTP |
|-----------------------------------|------|
| `auth.login.tenant_suspended` | 403 |
| `auth.login.tenant_closed` | 403 |

Otros relacionados útiles para UX: `auth.login.platform_blocked` (403), `auth.login.locked` (423), `auth.login.invalid` (401).

> Nota de naming: la guía histórica hablaba de `tenant.suspended`; el contrato expuesto es `auth.login.tenant_*` conforme `server/POS.API/Controllers/AuthController.cs`.

## Frontend

- Nueva utilidad `pos-frontend/src/app/core/auth/resolve-login-failure.ts` que interpreta `HttpErrorResponse` y prioriza **`error.message`** del envelope estándar.
- **`LoginComponent`**: muestra ese mensaje (no sólo “verificá credenciales”), con estilo distinto para errores de **ciclo de vida** del negocio (ámbar) vs credenciales (rojo) vs bloqueo plataforma (violeta).
- Banner de sesión de soporte (suplantación) ya estaba en `MainShell` / administración desde trabajo previo (Fase 7 en UI tenant).

## Verificación manual

1. Suspender tenant desde consola plataforma; intentar login con usuario del tenant → mensaje rojo/rosa no aplica por defecto: bloque ámbar con texto del servidor (“El negocio está suspendido…”).
2. Credenciales incorrectas → mensaje estándar de credenciales (401 sin body usable cae en fallback coherent).
