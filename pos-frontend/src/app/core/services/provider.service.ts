import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, catchError, Observable, of, switchMap } from 'rxjs';
import { Provider } from '../models/provider.model';

interface ApiErrorBody {
  code: string;
  message: string;
}

interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: ApiErrorBody | null;
}

export interface RequestResult<T> {
  success: boolean;
  data: T | null;
  error: ApiErrorBody | null;
  status: number;
  rawError?: unknown;
}

@Injectable({ providedIn: 'root' })
export class ProviderService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/providers';

  getAll(): Observable<readonly Provider[]> {
    return this.http
      .get<ApiResponse<readonly Provider[]>>(this.base)
      .pipe(map((r) => this.requireSuccessData(r)));
  }

  getById(id: string): Observable<Provider> {
    return this.http
      .get<ApiResponse<Provider>>(`${this.base}/${id}`)
      .pipe(map((r) => this.requireSuccessData(r)));
  }

  create(payload: {
    name: string;
    taxId: string;
    email: string;
    phone: string;
  }): Observable<RequestResult<Provider>> {
    return this.http.post<ApiResponse<Provider>>(this.base, payload).pipe(
      switchMap((response) => {
        if (!response.success || response.data === null) {
          return of(this.toFailure<Provider>(400, response.error));
        }
        return of({
          success: true,
          data: response.data,
          error: null,
          status: 201
        });
      }),
      catchError((e) => of(this.toHttpError<Provider>(e)))
    );
  }

  update(
    id: string,
    payload: { name: string; taxId: string; email: string; phone: string }
  ): Observable<RequestResult<Provider>> {
    return this.http.put<ApiResponse<Provider>>(`${this.base}/${id}`, payload).pipe(
      switchMap((response) => {
        if (!response.success || response.data === null) {
          return of(this.toFailure<Provider>(400, response.error));
        }
        return of({
          success: true,
          data: response.data,
          error: null,
          status: 200
        });
      }),
      catchError((e) => of(this.toHttpError<Provider>(e)))
    );
  }

  delete(id: string): Observable<RequestResult<void>> {
    return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`).pipe(
      switchMap((response) => {
        if (!response.success) {
          return of(this.toFailure<void>(400, response.error));
        }
        return of({ success: true, data: null, error: null, status: 200 });
      }),
      catchError((e) => of(this.toHttpError<void>(e)))
    );
  }

  private requireSuccessData<T>(response: ApiResponse<T>): T {
    if (!response.success || response.data === null) {
      throw new Error(response.error?.message ?? 'Error de proveedores.');
    }
    return response.data;
  }

  private toFailure<T>(status: number, error: ApiErrorBody | null): RequestResult<T> {
    return {
      success: false,
      data: null,
      error: error ?? { code: 'request.error', message: 'No se pudo completar la operación.' },
      status
    };
  }

  private toHttpError<T>(error: unknown): RequestResult<T> {
    if (!(error instanceof HttpErrorResponse)) {
      return this.toFailure<T>(500, { code: 'request.error', message: 'Error inesperado.' });
    }
    const body = error.error as { error?: ApiErrorBody; message?: string } | null;
    const apiError = body?.error ?? null;
    const message = body?.message ?? error.message ?? 'No se pudo completar la operación.';
    return {
      ...this.toFailure<T>(error.status || 500, apiError ?? { code: 'request.error', message }),
      rawError: error.error
    };
  }
}
