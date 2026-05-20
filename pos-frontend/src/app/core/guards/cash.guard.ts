import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { CashService } from '../services/cash.service';

/** Exige caja abierta (p. ej. para registrar ventas). Redirige a `/caja`. */
export const cashGuard: CanActivateFn = () => {
  const cash = inject(CashService);
  const router = inject(Router);
  return cash.refresh().pipe(
    map((has) => (has ? true : router.parseUrl('/caja'))),
    catchError(() => of(router.parseUrl('/caja')))
  );
};
