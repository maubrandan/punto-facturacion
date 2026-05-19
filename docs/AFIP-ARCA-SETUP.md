# AFIP/ARCA - Configuracion inicial (Fase 1)

Esta guia resume la configuracion base para facturacion electronica RI (Factura A/B) en el backend.

## Seccion de configuracion

Agregar en `server/POS.API/appsettings*.json`:

```json
"Arca": {
  "SandboxAutoApprove": true,
  "AuthorizationEndpoint": "https://arca.invalid/ws/authorize",
  "RetryMaxAttempts": 5,
  "RetryBaseDelaySeconds": 30,
  "RetryMaxDelayMinutes": 20
}
```

- `SandboxAutoApprove=true`: modo desarrollo para autorizar comprobantes sin llamada real.
- `AuthorizationEndpoint`: endpoint del adaptador ARCA cuando se desactive sandbox.
- Reintentos: controlan el worker que procesa `RetryScheduled`.

## Datos fiscales por tenant

Cada tenant requiere perfil fiscal habilitado en `TenantFiscalProfiles`:

- `TenantId`
- `TaxId` (CUIT sin separadores)
- `PointOfSale`
- `IsEnabled`
- `IsProduction`
- `CertificateRef` y `PrivateKeyRef` (referencias seguras, no secretos en claro)

## Endpoints API

- `POST /api/fiscal-documents/issue`
- `POST /api/fiscal-documents/retry`
- `POST /api/fiscal-documents/credit-note`

Todos usan el envelope estandar `ApiResponse<T>`.

## Notas operativas

- La emision es idempotente por `(TenantId, SaleId, DocumentType)`.
- Los errores transitorios programan reintentos con backoff exponencial.
- Las notas de credito solo se emiten si el comprobante origen esta autorizado.
