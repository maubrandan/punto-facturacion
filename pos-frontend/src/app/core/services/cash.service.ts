import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal, computed } from '@angular/core';
import { map, Observable, switchMap, tap } from 'rxjs';
import {
  CashSessionClose,
  CashSessionOpen,
  CashSessionSummary,
  ExpenseCategory,
  RegisterExpenseBody
} from '../models/cash.model';

interface ApiErrorBody {
  code: string;
  message: string;
}

interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: ApiErrorBody | null;
}

@Injectable({ providedIn: 'root' })
export class CashService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/cash';

  private readonly _summary = signal<CashSessionSummary | null>(null);

  /** Último resumen de la API; null hasta el primer `refresh`. */
  readonly summary = this._summary.asReadonly();

  readonly hasOpenSession = computed(() => {
    const s = this._summary();
    return s != null && s.sessionId != null;
  });

  /**
   * Carga resumen y devuelve true si hay sesión abierta (para el guard de ventas).
   */
  refresh(): Observable<boolean> {
    return this.http.get<ApiResponse<CashSessionSummary>>(`${this.base}/summary`).pipe(
      map((r) => this.requireSuccessData(r)),
      tap((s) => this._summary.set(s)),
      map((s) => s.sessionId != null)
    );
  }

  openSession(initialAmount: number): Observable<CashSessionOpen> {
    return this.http
      .post<ApiResponse<CashSessionOpen>>(`${this.base}/open`, { initialAmount })
      .pipe(
        map((r) => this.requireSuccessData(r)),
        switchMap((o) => this.refresh().pipe(map(() => o)))
      );
  }

  closeSession(countedAmount: number): Observable<CashSessionClose> {
    return this.http
      .post<ApiResponse<CashSessionClose>>(`${this.base}/close`, { countedAmount })
      .pipe(
        map((r) => this.requireSuccessData(r)),
        switchMap((o) => this.refresh().pipe(map(() => o)))
      );
  }

  getCategories(): Observable<readonly ExpenseCategory[]> {
    return this.http
      .get<ApiResponse<readonly ExpenseCategory[]>>(`${this.base}/categories`)
      .pipe(map((r) => this.requireSuccessData(r)));
  }

  registerExpense(body: RegisterExpenseBody): Observable<unknown> {
    return this.http.post<ApiResponse<unknown>>(`${this.base}/expenses`, body).pipe(
      map((r) => this.requireSuccessData(r)),
      switchMap((x) => this.refresh().pipe(map(() => x)))
    );
  }

  private requireSuccessData<T>(response: ApiResponse<T>): T {
    if (!response.success || response.data === null) {
      throw new Error(response.error?.message ?? 'Error de caja.');
    }
    return response.data;
  }
}
