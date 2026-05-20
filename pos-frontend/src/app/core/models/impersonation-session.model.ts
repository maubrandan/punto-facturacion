/** JWT de suplantación (soporte): claims `impersonation` e `imp_reason` (backend Fase 7). */
export interface ImpersonationSessionInfo {
  readonly reason: string;
}
