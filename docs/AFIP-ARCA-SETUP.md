# AFIP/ARCA - Configuracion inicial (Fase 1)

Esta guia resume la configuracion base para facturacion electronica RI (Factura A/B) en el backend.

## Seccion de configuracion

Agregar en `server/POS.API/appsettings*.json`:

```json
"Arca": {
  "SandboxAutoApprove": true,
  "EnableDirectAfip": false,
  "AuthorizationEndpoint": "https://arca.invalid/ws/authorize",
  "WsaaUrl": "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
  "WsfeUrl": "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
  "AfipServiceName": "wsfe",
  "CertificateBasePath": "C:/secrets/afip",
  "RetryMaxAttempts": 5,
  "RetryBaseDelaySeconds": 30,
  "RetryMaxDelayMinutes": 20
}
```

Modos de autorización (en orden de evaluación):

1. `SandboxAutoApprove=true` y perfil **no** en producción → CAE simulado (desarrollo).
2. `EnableDirectAfip=true` y certificado en perfil → WSAA + WSFEv1 directo contra AFIP/ARCA.
3. Caso contrario → `AuthorizationEndpoint` (adaptador HTTP externo).

Certificados en perfil fiscal:

- `CertificateRef`: ruta al `.pfx` (o `file:certificado.pfx` relativo a `CertificateBasePath`).
- `PrivateKeyRef`: contraseña del PFX, o ruta a clave PEM si el cert es `.crt`.

Homologación: URLs `wsaahomo` / `wswhomo`. Producción: marcar `IsProduction` en perfil y usar URLs de producción en config.

## Datos fiscales por tenant

Cada tenant requiere perfil fiscal habilitado en `TenantFiscalProfiles`:

- `TenantId`
- `TaxId` (CUIT sin separadores)
- `PointOfSale`
- `IsEnabled`
- `IsProduction`
- `CertificateRef` y `PrivateKeyRef` (referencias seguras, no secretos en claro)

## Endpoints API

Perfil fiscal (tenant):
- `GET /api/fiscal/profile`
- `PUT /api/fiscal/profile`

Perfil fiscal (plataforma, por tenant):
- `GET /api/platform/tenants/{tenantId}/fiscal-profile`
- `PUT /api/platform/tenants/{tenantId}/fiscal-profile` (requiere justificación)

Comprobantes:
- `GET /api/fiscal-documents/by-sale/{saleId}`
- `GET /api/fiscal-documents/{id}`
- `POST /api/fiscal-documents/issue` — body: `{ saleId, isInvoiceA, buyerTaxId?, buyerName? }`
- `POST /api/fiscal-documents/retry`
- `POST /api/fiscal-documents/credit-note`

El detalle de venta `GET /api/sales/{id}` incluye `fiscalDocuments` con CAE, QR AFIP y estado.

Todos usan el envelope estandar `ApiResponse<T>`.

## Notas operativas

- La emision es idempotente por `(TenantId, SaleId, DocumentType)`.
- Los errores transitorios programan reintentos con backoff exponencial.
- Las notas de credito solo se emiten si el comprobante origen esta autorizado.
