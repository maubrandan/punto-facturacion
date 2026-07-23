import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';

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
}

export interface StockLot {
  id: string;
  productId: string;
  lotNumber: string;
  expirationDate: string;
  quantity: number;
  isExpired: boolean;
}

export interface StockMovement {
  id: string;
  productId: string;
  productName: string;
  type: string;
  quantityDelta: number;
  quantityAfter: number;
  stockLotId: string | null;
  lotNumberSnapshot: string | null;
  expirationSnapshot: string | null;
  reasonCode: string | null;
  reasonNote: string | null;
  referenceId: string | null;
  createdAt: string;
}

export interface PagedStockMovements {
  items: StockMovement[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface StockAdjustmentResult {
  productId: string;
  stockAfter: number;
  stockLotId: string | null;
  lotNumber: string | null;
  quantityDelta: number;
  reasonCode: string;
}

export interface AdjustStockDto {
  productId: string;
  quantityDelta: number;
  reasonCode: string;
  note?: string | null;
  stockLotId?: string | null;
  lotNumber?: string | null;
  expirationDate?: string | null;
}

export interface AdjustmentReasonOption {
  code: string;
  label: string;
}

export interface ExpiryAlertItem {
  stockLotId: string;
  productId: string;
  productName: string;
  lotNumber: string;
  expirationDate: string;
  quantity: number;
  status: 'Expired' | 'ExpiringSoon' | string;
  daysToExpiration: number;
}

export interface ExpiryAlerts {
  supported: boolean;
  withinDays: number;
  items: ExpiryAlertItem[];
}

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/inventory';

  getLots(productId: string): Observable<readonly StockLot[]> {
    return this.http
      .get<ApiResponse<readonly StockLot[]>>(`${this.base}/products/${productId}/lots`)
      .pipe(map((r) => this.requireSuccess(r)));
  }

  getMovements(filters: {
    productId?: string;
    page?: number;
    pageSize?: number;
    from?: string;
    to?: string;
  }): Observable<PagedStockMovements> {
    let params = new HttpParams()
      .set('page', String(filters.page ?? 1))
      .set('pageSize', String(filters.pageSize ?? 20));
    if (filters.productId) {
      params = params.set('productId', filters.productId);
    }
    if (filters.from) {
      params = params.set('from', filters.from);
    }
    if (filters.to) {
      params = params.set('to', filters.to);
    }
    return this.http
      .get<ApiResponse<PagedStockMovements>>(`${this.base}/movements`, { params })
      .pipe(map((r) => this.requireSuccess(r)));
  }

  getAdjustmentReasons(): Observable<readonly AdjustmentReasonOption[]> {
    return this.http
      .get<ApiResponse<readonly AdjustmentReasonOption[]>>(`${this.base}/adjustment-reasons`)
      .pipe(map((r) => this.requireSuccess(r)));
  }

  getExpiryAlerts(withinDays = 30): Observable<ExpiryAlerts> {
    const params = new HttpParams().set('withinDays', String(withinDays));
    return this.http
      .get<ApiResponse<ExpiryAlerts>>(`${this.base}/expiry-alerts`, { params })
      .pipe(map((r) => this.requireSuccess(r)));
  }

  adjust(dto: AdjustStockDto): Observable<RequestResult<StockAdjustmentResult>> {
    return this.http.post<ApiResponse<StockAdjustmentResult>>(`${this.base}/adjustments`, dto).pipe(
      map((response) => {
        if (!response.success || response.data === null) {
          return this.failure<StockAdjustmentResult>(400, response.error);
        }
        return {
          success: true,
          data: response.data,
          error: null,
          status: 200
        };
      }),
      catchError((e: unknown) => of(this.httpError<StockAdjustmentResult>(e)))
    );
  }

  private requireSuccess<T>(response: ApiResponse<T>): T {
    if (!response.success || response.data === null) {
      throw new Error(response.error?.message ?? 'Error de inventario.');
    }
    return response.data;
  }

  private failure<T>(status: number, error: ApiErrorBody | null): RequestResult<T> {
    return {
      success: false,
      data: null,
      error: error ?? { code: 'request.error', message: 'No se pudo completar la operación.' },
      status
    };
  }

  private httpError<T>(error: unknown): RequestResult<T> {
    if (!(error instanceof HttpErrorResponse)) {
      return this.failure(500, { code: 'request.error', message: 'Error inesperado.' });
    }
    const body = error.error as { error?: ApiErrorBody; message?: string } | null;
    return this.failure(
      error.status || 500,
      body?.error ?? { code: 'request.error', message: body?.message ?? error.message }
    );
  }
}
