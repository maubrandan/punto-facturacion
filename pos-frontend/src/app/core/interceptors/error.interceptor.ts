import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { PlatformAuthService } from '../services/platform-auth.service';

/**
 * Extrae mensaje del envelope ApiResponse o del HttpErrorResponse.
 * En 401 limpia sesión tenant/platform según la URL.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const auth = inject(AuthService);
  const platformAuth = inject(PlatformAuthService);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse)) {
        return throwError(() => err);
      }

      const isPlatform = req.url.startsWith('/api/platform');
      const isAuthEndpoint =
        req.url.includes('/api/auth/login') ||
        req.url.includes('/api/auth/register') ||
        req.url.includes('/api/platform/auth/login');

      if (err.status === 401 && !isAuthEndpoint) {
        if (isPlatform) {
          platformAuth.logout();
          void router.navigateByUrl('/platform/login');
        } else {
          auth.logout();
          void router.navigateByUrl('/login');
        }
      }

      const body = err.error as { error?: { message?: string }; message?: string } | string | null;
      let message = err.message || 'Error de red.';
      if (typeof body === 'string' && body.trim()) {
        message = body;
      } else if (body && typeof body === 'object') {
        message = body.error?.message ?? body.message ?? message;
      }

      return throwError(() => new Error(message));
    })
  );
};
