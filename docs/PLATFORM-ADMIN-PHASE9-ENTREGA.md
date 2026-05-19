# Fase 9 — Entrega (backend): límites y flags por tenant

**Fecha:** 2026-04-30  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-9--límites-planes-y-feature-flags-domain--application).

## Resumen

- **Dominio:** entidad **`TenantEntitlement`** (clave `TenantId` FK a `Tenants`, `ON DELETE CASCADE`), campos opcionales `MaxProducts`, `MaxTenantUsers` (null = sin tope), `SalesEnabled` (flag global de ventas en POS), `UpdatedAtUtc`.
- **Migración:** `AddTenantEntitlements`.
- **Sin fila en BD:** comportamiento efectivo = sin límites numéricos y `salesEnabled: true`.
- **Plataforma:** `GET /api/platform/tenants/{tenantId}/entitlements` — policy **Platform.User** — `TenantEntitlementsDto`.
- **`PUT /api/platform/tenants/{tenantId}/entitlements`** — policy **Platform.Operations** — body `SetTenantEntitlementsApiRequest` (`maxProducts`, `maxTenantUsers`, `salesEnabled`, `justification` ≥ 5 caracteres); comando `SetTenantEntitlementsCommand` + `SetTenantEntitlementsCommandValidator`.
- **Servicio:** `IPlatformTenantEntitlementsService` / `PlatformTenantEntitlementsService` (consulta tenant vía `IPlatformDirectoryQuery`, auditoría `TenantEntitlementsUpdated`).
- **Enforcement (caliente):**
  - Alta de producto: `ITenantEntitlementGuard.EnsureCanCreateProductAsync` en `ProductsController` → código `entitlement.product_limit_reached`.
  - Venta: mismo guard en `CreateSaleHandler` después de validar caja abierta → `entitlement.sales_disabled`.
- **Usuarios `TenantUser`:** `EnsureCanAddTenantUserAsync(tenantId)` listo para el endpoint de alta de empleados (aún no expuesto tenant-side); cubierto por test contra el guard.

## Tests

- `TenantEntitlementsIntegrationTests`: tope productos, ventas deshabilitadas, límite de usuarios (guard), GET valores por defecto sin fila.

## Notas

- No existe **refresh token** en la API; la Fase 8 no tiene que revalidarlo aquí.
- Ampliaciones naturales: más flags, JSON de extensión validado o entidad **Plan** 1‑n tenants, y llamar explícitamente al guard cuando exista alta de usuarios dentro del mismo negocio.
