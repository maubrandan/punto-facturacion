# Fase 13 — Entrega incremental (backend + front v1)

**Fecha:** 2026-04-30  
Alineado con [PLATFORM-ADMIN-FASES](PLATFORM-ADMIN-FASES.md#fase-13--métricas-y-salud-observabilidad-de-producto).

## Resumen v1

- **Backend:** endpoint de agregados de plataforma `GET /api/platform/metrics/overview` protegido con `Platform.User`:
  - Totales de tenants por estado (`Active`, `Suspended`, `Closed`).
  - Usuarios de negocio con `BlockedByPlatform` (`AccountKind = TenantUser`).
  - Conteo de eventos `PlatformAuditEvents` en ventana UTC **últimas 24 h**.
  - Contrato `PlatformMetricsOverviewDto`, query `IPlatformMetricsOverviewQuery` e implementación en Infrastructure.
  - Cobertura: autorización (`PlatformApiAuthorizationTests`) + prueba funcional `PlatformMetricsOverviewIntegrationTests`.
- **Frontend:** página `/platform/dashboard` consume `getMetricsOverview()` y muestra tarjetas con esos KPIs más una tabla de muestra desde `GET /api/platform/audit` (primeras filas).

## Verificación local

```bash
dotnet test server/POS.API.IntegrationTests/POS.API.IntegrationTests.csproj --filter "FullyQualifiedName~PlatformMetricsOverviewIntegrationTests"
```

## Pendientes naturales

- KPIs más ricos **sin golpear OLTP pesado**: DAU, ventas / errores (`Fase 14` vistas o réplica), gráficas.
- Opcional: mismo endpoint exponiendo marca de tiempo de última compilación del agregado o `asOfUtc`.
