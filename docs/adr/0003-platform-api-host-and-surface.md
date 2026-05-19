# ADR 0003: Superficie HTTP y despliegue de la API de plataforma

- **Estado:** Aceptada  
- **Fecha:** 2026-04-29  
- **Ámbito:** API y despliegue — Fase 1 (diseño), Fase 4+ implementación

## Contexto

Hacer visible la consola de plataforma bajo el mismo origen que el POS simplifica CORS y despliegue, pero aumenta el riesgo de confundir rutas o reutilizar middleware pensado solo para `api/auth` y `api/*` multi-tenant.

## Decisión (fase actual: **MVP plataforma en el mismo host**)

1. **Prefijo unificado** para toda operación de plataforma:  
   **`/api/platform/**`**  
   - Nombres de controller o route group: `PlatformTenantsController` → ruta `api/platform/tenants` (nunca `api/tenants` sin `platform`).

2. **Mismo proceso ASP.NET** (`POS.API`) y **mismos** `Program.cs` / pipeline **salvo** que:
   - opcionalmente se registre un **contrato** `IPlatformRequestMarker` o middleware que trace `X-Request-Id` y etiquete logs con `source=platform`.

3. **Separar en despliegue a host distinto** (`api-admin.*`) queda como **evolución opcional** (Fase 14+): se documenta ahora para no acoplar el dominio a un origen. Los ADR 0001/0002 ya permiten el cambio (JWT por audience o reverse proxy) sin reescribir el dominio.

4. **Frontend:** aplicación o módulo Angular bajo ruta base **`/platform`**, con **build lazy**; host puede ser el mismo `pos-frontend` o proyecto separado en el mismo repositorio (monorepo). Misma regla: **nunca** montar la consola plataforma en `/dashboard` del tenant.

5. **CORS:** si en el futuro el admin vive en otro dominio, lista explícita de orígenes; mientras comparta origen, cookies no aplican (JWT en header, como ahora).

## Riesgos explícitos y mitigación (resumen)

| Riesgo | Mitigación |
|--------|------------|
| Fuga de acceso cruzado por path mal configurado | Revisar tests de ruta; prefijo obligatorio `api/platform`. |
| Operador con token tenant llama a `api/platform` | 403 por policy; tests de integración. |
| Suplantación sin trazas | Fase 7: motivo obligatorio + token efímero + ADR 0002. |
| Export / listados masivos | Rate limit, paginación máxima, Fase 10 (auditoría). |

## Consecuencias

- DevOps: un solo artefacto API en v1.  
- Seguridad: el cumplimiento fuerte pasa por **código y tests**, no por separación de red aún.

## Criterio de aceptación (Fase 4)

- Todos los controladores o endpoints de plataforma bajo `Route("api/platform/[controller]")` o equivalente minimal; documentación OpenAPI con tag "Platform".
