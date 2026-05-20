import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BehaviorSubject, catchError, map, Observable, of, switchMap, tap } from 'rxjs';
import { Product } from '../models/product.model';

export interface ProductUpsertDto {
  name: string;
  sku: string;
  barcode: string;
  netPrice: number;
  taxRate: number;
  stock: number;
  extendedDataJson?: string;
}

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
export class ProductService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = '/api/products';

  private readonly productsSubject = new BehaviorSubject<readonly Product[]>([]);

  // Expuesto como signal de solo lectura para UI reactiva.
  readonly products = toSignal(this.productsSubject.asObservable(), { initialValue: [] as readonly Product[] });

  getAll(): Observable<readonly Product[]> {
    return this.http
      .get<ApiResponse<readonly Product[]>>(this.apiBaseUrl)
      .pipe(
        map((response) => this.requireSuccessData(response)),
        tap((items) => this.productsSubject.next(items))
      );
  }

  getById(id: string): Observable<Product> {
    return this.http
      .get<ApiResponse<Product>>(`${this.apiBaseUrl}/${id}`)
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  /** Productos con menor stock del tenant (order ascendente, típicamente 5). */
  getLowStock(count = 5): Observable<readonly Product[]> {
    const params = new HttpParams().set('count', String(count));
    return this.http
      .get<ApiResponse<readonly Product[]>>(`${this.apiBaseUrl}/low-stock`, { params })
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  create(payload: ProductUpsertDto): Observable<RequestResult<Product>> {
    return this.http
      .post<ApiResponse<Product>>(this.apiBaseUrl, payload)
      .pipe(
        switchMap((response) => {
          if (!response.success || response.data === null) {
            return of(this.toFailureResult<Product>(400, response.error));
          }

          return this.getAll().pipe(
            map(() => ({
              success: true,
              data: response.data,
              error: null,
              status: 201
            }))
          );
        }),
        catchError((error) => of(this.toHttpErrorResult<Product>(error)))
      );
  }

  update(id: string, payload: ProductUpsertDto): Observable<RequestResult<Product>> {
    return this.http
      .put<ApiResponse<Product>>(`${this.apiBaseUrl}/${id}`, payload)
      .pipe(
        switchMap((response) => {
          if (!response.success || response.data === null) {
            return of(this.toFailureResult<Product>(400, response.error));
          }

          return this.getAll().pipe(
            map(() => ({
              success: true,
              data: response.data,
              error: null,
              status: 200
            }))
          );
        }),
        catchError((error) => of(this.toHttpErrorResult<Product>(error)))
      );
  }

  delete(id: string): Observable<RequestResult<void>> {
    return this.http
      .delete<ApiResponse<null>>(`${this.apiBaseUrl}/${id}`)
      .pipe(
        switchMap((response) => {
          if (!response.success) {
            return of(this.toFailureResult<void>(400, response.error));
          }

          return this.getAll().pipe(
            map(() => ({
              success: true,
              data: null,
              error: null,
              status: 200
            }))
          );
        }),
        catchError((error) => of(this.toHttpErrorResult<void>(error)))
      );
  }

  private requireSuccessData<T>(response: ApiResponse<T>): T {
    if (!response.success || response.data === null) {
      throw new Error(response.error?.message ?? 'No se pudo completar la operación de productos.');
    }

    return response.data;
  }

  private toFailureResult<T>(status: number, error: ApiErrorBody | null): RequestResult<T> {
    return {
      success: false,
      data: null,
      error: error ?? { code: 'request.error', message: 'No se pudo completar la operación.' },
      status,
      rawError: null
    };
  }

  private toHttpErrorResult<T>(error: unknown): RequestResult<T> {
    if (!(error instanceof HttpErrorResponse)) {
      return this.toFailureResult<T>(500, { code: 'request.error', message: 'Error inesperado.' });
    }

    const body = error.error as { error?: ApiErrorBody; message?: string } | null;
    const apiError = body?.error ?? null;
    const fallbackMessage = body?.message ?? error.message ?? 'No se pudo completar la operación.';

    return {
      ...this.toFailureResult<T>(
      error.status || 500,
      apiError ?? { code: 'request.error', message: fallbackMessage }
      ),
      rawError: error.error
    };
  }
}
