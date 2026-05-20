/**
 * Preferencia persistida por usuario y tenant ({@link uiDensityScopedStorageKey}).
 * Esta clave antigua única rompe el aislamiento entre cuentas en el mismo navegador;
 * debe borrarse al cerrar sesión (ver AuthService.logout).
 */
export const UI_DENSITY_LEGACY_STORAGE_KEY = 'ui-density';

export function uiDensityScopedStorageKey(params: {
  tenantId: string;
  userId: string;
}): string {
  return `ui-density:${params.tenantId}:${params.userId}`;
}
