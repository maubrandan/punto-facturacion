import { HttpErrorResponse } from '@angular/common/http';

/** Cómo presentar el error en pantalla de login POS (Fase 12 — transparencia tenant). */
export type LoginErrorKind = 'lifecycle' | 'platform_block' | 'locked' | 'invalid' | 'generic';

export interface LoginFailurePresentation {
  message: string;
  kind: LoginErrorKind;
}

interface ApiEnvelope {
  success?: boolean;
  error?: { code?: string; message?: string } | null;
}

function parseApiEnvelope(payload: unknown): ApiEnvelope | null {
  if (!payload || typeof payload !== 'object') {
    return null;
  }
  const o = payload as Record<string, unknown>;
  const err = o['error'];
  if (!err || typeof err !== 'object') {
    return { success: typeof o['success'] === 'boolean' ? o['success'] : undefined, error: null };
  }
  const e = err as Record<string, unknown>;
  const code = typeof e['code'] === 'string' ? e['code'] : '';
  const message = typeof e['message'] === 'string' ? e['message'] : '';
  return {
    success: typeof o['success'] === 'boolean' ? o['success'] : undefined,
    error: { code, message },
  };
}

function kindFromErrorCode(code: string | undefined): LoginErrorKind | null {
  switch (code) {
    case 'auth.login.tenant_suspended':
    case 'auth.login.tenant_closed':
      return 'lifecycle';
    case 'auth.login.platform_blocked':
      return 'platform_block';
    case 'auth.login.locked':
      return 'locked';
    case 'auth.login.invalid':
      return 'invalid';
    default:
      return null;
  }
}

/**
 * Interpreta fallos de `POST /api/auth/login`: prioriza mensaje estándar del backend en envelope.
 */
export function resolveLoginFailure(err: unknown): LoginFailurePresentation {
  if (err instanceof HttpErrorResponse) {
    const envelope = parseApiEnvelope(err.error);
    const code = envelope?.error?.code?.trim();
    const apiMessage = envelope?.error?.message?.trim();

    const fromCode = kindFromErrorCode(code);
    if (apiMessage && fromCode) {
      return { message: apiMessage, kind: fromCode };
    }

    if (apiMessage) {
      return { message: apiMessage, kind: 'generic' };
    }

    if (err.status === 401) {
      return {
        message: 'Email o contraseña incorrectos, o la cuenta no está confirmada.',
        kind: 'invalid',
      };
    }
    if (err.status === 403) {
      return {
        message: 'Acceso denegado. Si el problema continúa, contactá a soporte.',
        kind: 'lifecycle',
      };
    }
    if (err.status === 423) {
      return {
        message: 'La cuenta está bloqueada temporalmente. Probá de nuevo más tarde.',
        kind: 'locked',
      };
    }

    return {
      message: 'No se pudo iniciar sesión. Probá de nuevo en unos minutos.',
      kind: 'generic',
    };
  }

  if (err instanceof Error && err.message) {
    return { message: err.message, kind: 'generic' };
  }

  return {
    message: 'No se pudo iniciar sesión. Probá de nuevo en unos minutos.',
    kind: 'generic',
  };
}
