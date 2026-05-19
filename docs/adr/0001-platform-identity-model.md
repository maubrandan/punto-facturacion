# ADR 0001: Modelo de identidad para operadores de plataforma

- **Estado:** Aceptada  
- **Fecha:** 2026-04-29  
- **Ámbito:** Admin de plataforma (cross-tenant) — Fase 1 (diseño), implementación Fase 2–3

## Contexto

Hoy `ApplicationUser` exige `TenantId` y el emisor de JWT (`JwtTokenService`) **rechaza** usuarios sin `TenantId`. Los operadores de plataforma deben actuar **sobre cualquier tenant** o sobre metadatos globales (tenants, auditoría) **sin** pertenecer a un único comercio como un cajero.

Opciones consideradas:

1. **Mismo almacén Identity, usuario “sin tenant” o tenant sentinela** (p. ej. `TenantId` vacío o GUID fijo reservado `00000000-0000-0000-0000-000000000000` = “plataforma”) + claims de rol.
2. **Tabla/entidad separada** `PlatformUser` sin relación 1:1 con `ApplicationUser` tenant.
3. **Doble identidad:** cuentas distintas (email + password) para POS vs plataforma.

## Decisión

1. **Persistir operadores de plataforma en el mismo Identity store** (`ApplicationUser` / ASP.NET Core Identity) para unificar lockout, password hashing y auditoría de inicio de sesión.

2. **Distinguir el tipo de cuenta** mediante:
   - un campo explícito en dominio, p. ej. `AccountKind` = `TenantUser` | `PlatformUser` (o booleano `IsPlatformOperator` con semántica clara en `POS.Domain`), **y**
   - **roles** de Identity con nombres reservados bajo un prefijo, p. ej. `Platform.SuperAdmin`, `Platform.Operations`, `Platform.SupportReadOnly`.

3. **Para `TenantId` en usuarios de plataforma:** usar un **tenant sentinela** documentado (GUID fijo reservado) **o** `null` en columna nullable, según se implemente en Fase 3. **No** reutilizar un `TenantId` de un negocio real. La emisión de JWT (ADR 0002) **no** replicará ese sentinela como “tenant operativo” en claims de negocio: se emitirá claim explícita `is_platform` / `platform_scope`.

4. Un usuario **no** puede ser simultáneamente operador de plataforma y usuario operativo de un tenant en la **misma** sesión: dos tokens distintos (login POS vs login consola plataforma). (Suplantación controlada es un tercer flujo en Fase 7.)

## Consecuencias

- **Positivas:** Un solo mecanismo de autenticación; roles alineados con `AddIdentityCore`; policies ASP.NET por nombre de rol/claim.
- **Negativas:** Migración EF: alterar `ApplicationUser` o añadir tabla de extensión; cuidar seed y **no** mezclar en UI el login de cajero con el de plataforma.
- **Hoy (código existente):** `JwtTokenService` sigue asumiendo `TenantId` obligatorio; la Fase 2+ introducirá rama o servicio `CreatePlatformToken` con validaciones distintas (ver ADR 0002).

## Criterio de aceptación (Fase 2)

- Entidad o propiedad de dominio documentada; invariante: `TenantUser` tiene `TenantId` de negocio; `PlatformUser` no actúa como membership de un solo tenant en reglas de negocio del POS.
