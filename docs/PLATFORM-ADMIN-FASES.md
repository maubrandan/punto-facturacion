# Plan por fases: Admin de plataforma (100 % funcional y maduro)

Este documento define un **mapa de entrega por fases** para un **admin de plataforma** (operaciones sobre **todos los tenants** y el sistema), alineado con **Clean Architecture + DDD** (`.cursor/agents/clean-architecture-ddd.md` y `dotnet-clean-architecture.mdc`).

> **Contexto del repo hoy:** API plataforma incluye usuarios por tenant; **auditoría persistente** (Fase 10) implementada en backend y consultable desde consola; **front** consola en `pos-frontend` (`/platform/*`) incluye KPIs incrementales (**Fase 13** primer entrega).

> **Sobre el “100 %”:** en software, “completo al 100 %” se traduce en **criterios de aceptación verificables** por fase, más **cierre formal** (Fase 15). Nuevas exigencias legales o de producto añaden iteraciones.

---

## Principios transversales (aplican a todas las fases)

| Principio | Implicación |
|------------|-------------|
| **Separación estricta** | La API y la UI de **plataforma** no comparten rutas con el **POS tenant** salvo que esté justificado, auditado y acotado en tiempo. |
| **Domain primero** | Nuevas entidades/reglas van a `POS.Domain`; casos de uso a `POS.Application` (comandos/consultas, validación, `Result`); API delgada. |
| **Multi-tenancy** | Cualquier operación “sobre un tenant concreto” recibe el **TenantId** como parámetro explícito; los handlers validan existencia y estado. **Nunca** asumir el tenant del JWT de un usuario de plataforma salvo en flujos documentados (p. ej. suplantación). |
| **Auditoría** | Toda mutación o acceso sensible deja **trazabilidad** (quién, qué, cuándo, tenant afectado, motivo, correlación). |
| **Mínimo privilegio** | Roles y políticas de plataforma granulares (p. ej. `PlatformSupportReadOnly` vs `PlatformAdmin`). |

---

## Fase 0 — Línea base y deuda hacia DDD (opcional pero recomendada)

**Objetivo:** Alinear el backend con el stack descrito en la guía (CQRS con **MediatR**, **FluentValidation**, handlers explícitos) *antes* o *en paralelo* con el primer endpoint de plataforma, para no duplicar patrones.

| Entregable | Criterio de aceptación |
|------------|------------------------|
| Paquetes y registro de **MediatR** en `DependencyInjection` | Un comando existente (p. ej. registrar venta) pasa a handler MediatR sin romper contrato API. |
| **FluentValidation** para al menos un comando de referencia | Validación sale del controller. |
| Convención de nombres `Commands/` `Queries/` `Behaviors/` | Documentada en 1 párrafo en este doc o en README de `server/`. |

**Salida de fase:** Una “vertical de referencia” end-to-end con el patrón deseado; el admin de plataforma **no** se implementa aún (solo cimientos).

---

## Fase 1 — Modelo de producto: qué es “plataforma” (diseño)

**Estado:** Completada (diseño documentado, sin código de producción obligatorio).  
**Entregables aglutinados:** [PLATFORM-ADMIN-PHASE1.md](PLATFORM-ADMIN-PHASE1.md) · [docs/adr/](adr/README.md)

**Objetivo:** Congelar decisiones que si no, ensucian el dominio.

| Tema | Decisiones a tomar (checklist) |
|------|---------------------------------|
| **Identidad** | ¿Usuario con **rol global** (sin tenant) vs. usuario con `TenantId` + claim `Platform*`? |
| **Claims JWT** | Lista mínima: `platform_role`, `tenant_id` solo en contexto tenant; para plataforma, `aud` o claim explícito `is_platform` |
| **Aislamiento de despliegue** | Misma app API con prefijo `api/platform/*` vs. **API host separado** (más seguro) |
| **Riesgos** | Listado: suplantación, export masivo, borrado lógico, fuerza bruta a cuentas plataforma |

| Entregable | Criterio de aceptación |
|------------|------------------------|
| **ADRs** (1–3 archivos) en `docs/adr/` o anexos breves | Decisiones firmadas: modelo de identidad, forma del JWT, superficie de API. |
| **Matriz de roles** plataforma (RBAC) | Tabla: rol → permisos → endpoints. |
| **Wireframes o mapa de pantallas** | Navegación mínima del front de plataforma. |

**Salida de fase:** Documento aprobado; **código opcional** (puede ser 0 archivos de producción).

---

## Fase 2 — Dominio: identidad y contexto de plataforma (Domain + Application)

**Estado:** Completada en backend (modelo, comandos, handler sin HTTP público; ver [PLATFORM-ADMIN-PHASE2-ENTREGA.md](PLATFORM-ADMIN-PHASE2-ENTREGA.md)).  
**Objetivo:** Modelar en **Domain** lo que es “operador de plataforma” sin mezclarlo con `ITenantEntity` de forma ambigua.

| Entregable | Criterio de aceptación |
|------------|------------------------|
| **Entidad / agregado** p. ej. `PlatformUser`, `PlatformRole` o extensión documentada de Identity con **invariantes** | Sin referencias a EF. |
| **Value objects** si aplica | Email de contacto, motivo de auditoría, etc. |
| **Interfaces** en `Application` | p. ej. `IPlatformUserRepository`, `IPlatformAuditService` (contrato, no implementación). |
| **Comandos iniciales** (sin HTTP aún) | p. ej. `ProvisionPlatformUserCommand` con `FluentValidation` + `Result`. |

**Salida de fase:** Compila; tests unitarios de dominio opcionales pero deseables (reglas puras).

---

## Fase 3 — Infraestructura: persistencia, Identity y filtros seguros (Infrastructure)

**Estado:** Completada en backend (consulta directorio, extensiones EF, seed opcional plataforma, reglas de `SaveChanges` alineadas con Identity; ver [PLATFORM-ADMIN-PHASE3-ENTREGA.md](PLATFORM-ADMIN-PHASE3-ENTREGA.md) y [PLATFORM-QUERY-FILTERS.md](PLATFORM-QUERY-FILTERS.md)). La migración `AccountKind` y roles `Platform.*` encajan con lo entregado en Fase 2 — [PLATFORM-ADMIN-PHASE2-ENTREGA.md](PLATFORM-ADMIN-PHASE2-ENTREGA.md).

**Objetivo:** Que EF y Identity soporten usuarios/roles de plataforma y que **ningún** query filter de tenant fuga datos entre tenants *por error* al operar en modo plataforma.

| Entregable | Criterio de aceptación |
|------------|------------------------|
| **Estrategia de query filters** | Documentar dónde se **omite** el filtro (solo en repositorios dedicados plataforma) y bajo qué pruebas. |
| **Migración EF** | Tablas/relaciones o claims persistidos. |
| **Seeding** mínimo | Uso solo en entornos controlados (dev/staging); nunca producción con credenciales fijas en código. |

**Salida de fase:** Migración aplicable; usuario plataforma en **Development** habilitable vía `PlatformAdminSeed` en configuración (opcional) y documentado en la entrega de fase.

---

## Fase 4 — API: superficie aislada `api/platform/...` (API)

**Estado:** Completada en backend (ver [PLATFORM-ADMIN-PHASE4-ENTREGA.md](PLATFORM-ADMIN-PHASE4-ENTREGA.md)).  
**Objetivo:** Endpoints con **autorización dedicada** y el mismo **envelope** `ApiResponse<T>`.

| Grupo de endpoints (ejemplos) | Criterio |
|--------------------------------|----------|
| `GET /api/platform/health` o `.../version` | Sin tenant; responde OK con versión. |
| `GET /api/platform/tenants` (paginado) | Solo rol plataforma; DTOs sin secretos. |
| **Policies** | `Platform.User`, `Platform.ReadOnly`, `Platform.Operations`, `Platform.SuperAdmin` (ver `AuthorizationPolicies`). |

| Entregable | Criterio de aceptación |
|------------|------------------------|
| **Filtro o middleware** opcional de correlación / logging | Cada request lleva cabecera de respuesta `X-Request-Id` (generada si falta). |
| **No lógica de negocio** en el controller | Delegación en `IAuthService` / `IPlatformDirectoryQuery` (MediatR opcional en Fase 0). |

**Salida de fase:** OpenAPI en desarrollo con `[Tags("Platform")]`; pruebas de integración **401 / 403 / 200** en `PlatformApiAuthorizationTests`.

---

## Fase 5 — Gestión de tenants (ciclo de vida completo) (Application + API)

**Estado:** Completada en backend (ver [PLATFORM-ADMIN-PHASE5-ENTREGA.md](PLATFORM-ADMIN-PHASE5-ENTREGA.md)).  
**Objetivo:** CRUD de `Tenant` y estados: **Activo, Suspendido, Cerrado/Archivado** (definir máquina de estados en Domain).

| Casos de uso | Criterio |
|--------------|----------|
| **Crear tenant** con nombre, identificador, contacto (VOs) | Validación + evento de dominio opcional `TenantCreated`. |
| **Actualizar** datos de facturación/comercial (si aplica) | Invariante: nombres no vacíos, límites. |
| **Suspender** | Bloquea login de usuarios del tenant o devuelve error de negocio; mensaje al usuario final coordinado. |
| **Cerrar** (soft delete) | Idempotente; implica políticas en Auth (Fase 6/8). |

| Entregable | Criterio de aceptación |
|------------|------------------------|
| **Listado paginado y filtros** | Por nombre, estado, fecha. |
| **Pruebas de integración** | al menos: crear, suspender, listar. |

**Salida de fase:** Migración `AddTenantLifecycle`; endpoints bajo `api/platform/tenants` con policies finas; tests en `PlatformTenantLifecycleIntegrationTests`.

---

## Fase 6 — Usuarios y acceso: operaciones cross-tenant (Application + API)

**Estado:** Completada en backend (ver [PLATFORM-ADMIN-PHASE6-ENTREGA.md](PLATFORM-ADMIN-PHASE6-ENTREGA.md)).  
**Objetivo:** Que soporte/operaciones gestione **usuarios** ligados a tenants (invitar, reset, bloqueo) *sin* abrir un agujero de seguridad.

| Caso de uso | Criterio |
|-------------|----------|
| **Listar usuarios de un tenant** (desde plataforma) | Con auditoría. |
| **Reenvío de invitación** / forzar **reset** | Flujo que no exponga el password; alinea con `AuthController` o servicio de correo. |
| **Bloquear** usuario a nivel plataforma | Distinguir de “usuario bloqueado en tenant” si el modelo lo requiere. |

**Restricción de seguridad:** toda acción mutadora requiere **justificación** en el cuerpo (`PlatformUserActionRequest`); se propaga a `PlatformAuditEventData.Justification` (persistencia en Fase 10).

**Salida de fase:** Migración `AddApplicationUserBlockedByPlatform`; rutas `api/platform/tenants/{tenantId}/users/*`; tests `PlatformTenantUsersIntegrationTests`.

---

## Fase 7 — “Ver como / Suplantación controlada” (opcional pero típica en plataformas)

**Estado (backend):** entregado — ver [PLATFORM-ADMIN-PHASE7-ENTREGA](PLATFORM-ADMIN-PHASE7-ENTREGA.md).

**Objetivo:** Un operador con permiso **Support** abre el POS **en contexto de un tenant** con token de duración **corta** y trazas explícitas.

| Entregable | Criterio de aceptación |
|------------|------------------------|
| **Comando** `ImpersonationSessionStart` con motivo, TTL | Result con token efímero o one-time. |
| **Prohibición** de reutilizar suplantación en rutas de plataforma | Tests. |
| **Aviso en UI** (POS) “Sesión de soporte” (front tenant en Fase 12) | Visible para el cajero (transparencia). |

**Nota de diseño:** Alternativa “solo lectura” (sin suplantación) es más simple y más segura para MVP+.

---

## Fase 8 — Cumplimiento de Auth para tenant suspendido/cerrado

**Estado (backend):** entregado — ver [PLATFORM-ADMIN-PHASE8-ENTREGA](PLATFORM-ADMIN-PHASE8-ENTREGA.md).

**Objetivo:** Cualquier `Login` o refresh para usuarios cuyo tenant no está **Activo** falla con código de negocio claro (p. ej. `tenant.suspended`).

| Entregable | Criterio de aceptación |
|------------|------------------------|
| Cambio en `IAuthService` o handler de login | Rechazo consistente con `ApiResponse`. |
| **Tests de integración** | Usuario de tenant suspendido no obtiene token. |

---

## Fase 9 — Límites, planes y feature flags (Domain + Application)

**Estado (backend v1):** entregado — ver [PLATFORM-ADMIN-PHASE9-ENTREGA](PLATFORM-ADMIN-PHASE9-ENTREGA.md).

**Objetivo:** La plataforma gobierna qué puede hacer cada tenant (aunque sea v1: solo flags booleanos o números).

| Entregable | Criterio de aceptación |
|------------|------------------------|
| Entidad **Plan** o **Entitlement** | Relación 1..n o JSON validado. |
| **Aplicación en caliente** o en próxima acción (documentar) | p. ej. límite de productos, de usuarios. |
| **Comandos** de plataforma | `SetTenantEntitlements`, `SetFeatureFlag`. |

**Enforcement:** Los casos de uso *tenant* consultan el entitlement (o servicio de aplicación) al crear entidades costosas (evitar lógica solo en front).

---

## Fase 10 — Auditoría inmutable (append-only) y export

**Estado (backend v1):** entregado — ver [PLATFORM-ADMIN-PHASE10-ENTREGA](PLATFORM-ADMIN-PHASE10-ENTREGA.md).

**Objetivo:** Trazabilidad exigible en auditoría externa o soportes internos.

| Entregable | Criterio de aceptación |
|------------|------------------------|
| Tabla **PlatformAuditEvent** o similar | Campos: actor, acción, tenant afectado, payload resumido, IP, `CorrelationId`, timestamp UTC. |
| **Sin UPDATE/DELETE** en filas de auditoría | Solo inserción. |
| **GET** paginado + filtros | Por tenant, por actor, rango de fechas. |
| **Export** CSV/JSON restringido a rol adecuado | Rate limit (Fase 4/14). |

---

## Fase 11 — Front de plataforma: aplicación o módulo dedicado (Angular)

**Estado (frontend v1.1):** entregado en `pos-frontend` bajo rutas `/platform/*` con detalle tenant + bloque de usuarios tenant (listar/bloquear/desbloquear/reset/reenvío) — ver [PLATFORM-ADMIN-PHASE11-ENTREGA](PLATFORM-ADMIN-PHASE11-ENTREGA.md).

**Objetivo:** **No** reutilizar el `MainShell` del POS para operadores de plataforma; reduce errores (venta accidental en nombre del sistema) y mejora UX de “consola”.

| Entregable | Criterio de aceptación |
|------------|------------------------|
| **Rutas bajo** `/platform` o **subdominio** | Lazy loading. |
| **Tema visual distinto** (colores, logo) | Identificable. |
| **Autenticación** | Login dedicado o mismo login con flujo a consola plataforma si el JWT incluye `is_platform` |
| **Pantallas mínimas** v1 | Dashboard plataforma, listado de tenants, detalle tenant, auditoría, usuarios. |
| **Guards** | `platformGuard` que exige claim/policy. |

Estructura sugerida: `pos-frontend/src/app/features/platform-admin/` (carpeta nueva; la carpeta `features/admin` del POS tenant puede reservarse para *admin de negocio* o renombrarse según Fase 1).

---

## Fase 12 — Ajustes en front tenant (transparencia)

**Estado:** entrega front alineada a códigos `auth.login.tenant_*` — ver [PLATFORM-ADMIN-PHASE12-ENTREGA](PLATFORM-ADMIN-PHASE12-ENTREGA.md).

**Objetivo:** Si aplica Fase 7, el POS **muestra** banner de suplantación; si aplica cierre/suspensión, mensajes alineados con códigos de error del envelope.

| Entregable | Criterio mínimo |
|------------|-----------------|
| Manejo de suspensión/cierre en login (`auth.login.tenant_suspended`, `auth.login.tenant_closed`) | Mensaje claro (texto backend + UX distinta a credencial inválida). |

---

## Fase 13 — Métricas y salud (observabilidad de producto)

**Estado (incremental):** KPIs globales backend + dashboard plataforma v1 — ver [PLATFORM-ADMIN-PHASE13-ENTREGA](PLATFORM-ADMIN-PHASE13-ENTREGA.md).

**Objetivo:** Vista plataforma de “cómo va” el ecosistema.

| Entregable | Criterio |
|------------|----------|
| **Agregados** (DAU, ventas 24h, error rate por tenant) vía reportes/Queries | Sin impactar OLTP: lecturas desde DB réplica o vistas en Fase 14. |
| **Panel** en plataforma | gráficos o tablas. |

---

## Fase 14 — Datos, rendimiento y operación

**Objetivo:** Soportar listados grandes, export y cumplir SLAs de consulta.

| Tema | Acción |
|------|--------|
| **Índices** en `Tenant`, `User`, `PlatformAuditEvent` | Por filtros frecuentes. |
| **Vistas materializadas** o jobs de resumen (opcional) | Para Fase 13. |
| **Límites** de `pageSize` y timeouts | En handlers de listado. |

---

## Fase 15 — Cierre “madurez plataforma” (seguridad, legal, runbooks)

**Objetivo:** Criterio de aceptación global del programa.

| Control | Criterio |
|---------|----------|
| **Revisión de seguridad** (OWASP, roles, CORS) | Registro de hallazgos y mitigación. |
| **Política de retención** de auditoría y GDPR (si aplica) | DPA / borrado: definido, aunque implementación mínima. |
| **Runbook** operación | Cómo suspender un tenant, cómo rotar claves plataforma, contacto. |
| **Criterio 100 % de esta fase** | Checklist firmado: todas las fases 4–10 y 11 mínimamente usables en **staging** con E2E manual o automatizado. |

---

## Resumen: orden de ejecución sugerido

0 → 1 (diseño) → 2 → 3 → 4 → 5 → 8 (auth tenant status) en paralelo cercano al 5 → 6 → 9 → 10 → 11 → 12 → 13 → 14 → 15.  
Fases **7** y **0** son opcionales en posición, pero **0** al principio ahorra retrabajo de patrones.

---

## Riesgo explícito

Implementar “consola con listado de tenants” **sin** Fases 2–3 (modelo e Identity) y **10** (auditoría) produce un producto difícil de sostener bajo la guía DDD; las fases 4–5 sin 8 dejan el sistema en estado **incoherente** (tenant suspendido pero usuarios aún con token).

---

*Documento vivo: actualizar con ADRs reales y fechas de hito al comenzar cada fase.*
