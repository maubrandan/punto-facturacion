import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { finalize, map } from 'rxjs/operators';
import type { FiscalDocumentView } from '../models/fiscal.model';
import { ProductService, RequestResult } from './product.service';

export interface CreateSaleLineDto {
  productId: string;
  quantity: number;
  stockLotId?: string | null;
}

export interface CreateSalePaymentDto {
  method: number;
  amount: number;
}

export interface CreateSaleDto {
  lines: CreateSaleLineDto[];
  payments: CreateSalePaymentDto[];
  customerId?: string | null;
}

export interface SaleLineResponse {
  id: string;
  productId: string;
  productName: string;
  productExtendedDataJson: string;
  quantity: number;
  lineNetSubtotal: number;
  lineTaxAmount: number;
  unitNetPrice: number;
  taxRate: number;
  stockLotId?: string | null;
  lotNumber?: string | null;
}

export interface SaleSummaryRow {
  id: string;
  fecha: string;
  total: number;
  usuarioNombre: string;
  cantidadItems: number;
}

export interface PagedSalesResult {
  items: SaleSummaryRow[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface SaleDetailLineView {
  id: string;
  productId: string;
  productName: string;
  productExtendedDataJson: string;
  quantity: number;
  lineNetSubtotal: number;
  lineTaxAmount: number;
  lineTotal: number;
  stockLotId?: string | null;
  lotNumber?: string | null;
}

export interface SaleDetailView {
  id: string;
  date: string;
  totalNet: number;
  totalTax: number;
  totalAmount: number;
  createdByUserName: string | null;
  lines: SaleDetailLineView[];
  payments: SalePaymentView[];
  fiscalDocuments?: FiscalDocumentView[];
  /** 0 = None, 1 = FullyReturned */
  returnStatus?: number;
  return?: SaleReturnView | null;
}

export interface SaleReturnLineView {
  id: string;
  saleDetailId: string;
  productId: string;
  productName: string;
  quantity: number;
  stockLotId?: string | null;
  lineNetSubtotal: number;
  lineTaxAmount: number;
}

export interface SaleReturnView {
  id: string;
  saleId: string;
  returnedAt: string;
  totalAmount: number;
  createdByUserName: string | null;
  cashSessionId?: string | null;
  fiscalDocumentId?: string | null;
  lines: SaleReturnLineView[];
  payments: SalePaymentView[];
}

export interface SalePaymentView {
  id: string;
  method: number;
  amount: number;
}

export interface DailySummaryResult {
  totalFacturado: number;
  ventasCount: number;
  topProductId: string | null;
  topProductName: string | null;
  topProductUnits: number;
}

export interface SalesReportPaymentBreakdown {
  method: number;
  amount: number;
  paymentCount: number;
}

export interface SalesReportCashierBreakdown {
  createdByUserId: string | null;
  createdByUserName: string;
  totalAmount: number;
  salesCount: number;
}

export interface SalesReportResult {
  startDate: string;
  endDate: string;
  totalSalesAmount: number;
  salesCount: number;
  byPaymentMethod: SalesReportPaymentBreakdown[];
  byCashier: SalesReportCashierBreakdown[];
}

export interface MarginReportSkuItem {
  productId: string;
  sku: string;
  productName: string;
  quantity: number;
  revenueNet: number;
  costNet: number | null;
  marginNet: number | null;
  hasCost: boolean;
}

export interface MarginReportResult {
  startDate: string;
  endDate: string;
  revenueNet: number;
  revenueNetWithCost: number;
  revenueNetWithoutCost: number;
  costNet: number;
  marginNet: number;
  linesWithCost: number;
  linesWithoutCost: number;
  bySku: MarginReportSkuItem[];
}

export type TopSkusSortBy = 'quantity' | 'revenue';

export interface TopSkuReportItem {
  productId: string;
  sku: string;
  productName: string;
  quantity: number;
  revenueNet: number;
  revenueTotal: number;
}

export interface TopSkusReportResult {
  startDate: string;
  endDate: string;
  sortBy: TopSkusSortBy | string;
  items: TopSkuReportItem[];
}

export type SalesPeriod = 'day' | 'week' | 'month';

export interface SalesByPeriodBucketItem {
  periodStart: string;
  periodEnd: string;
  totalSalesAmount: number;
  salesCount: number;
}

export interface SalesByPeriodReportResult {
  startDate: string;
  endDate: string;
  period: SalesPeriod | string;
  totalSalesAmount: number;
  salesCount: number;
  buckets: SalesByPeriodBucketItem[];
}

export interface SaleResponse {
  id: string;
  date: string;
  totalNet: number;
  totalTax: number;
  totalAmount: number;
  lines: SaleLineResponse[];
  payments: SalePaymentView[];
}

/**
 * Convierte la respuesta de {@link SaleService.create} al formato de detalle / ticket.
 * El POST no incluye el nombre del cajero; pasar p. ej. email del usuario actual o `null`.
 */
export function saleResponseToDetailView(
  s: SaleResponse,
  createdByUserName: string | null = null
): SaleDetailView {
  return {
    id: s.id,
    date: s.date,
    totalNet: s.totalNet,
    totalTax: s.totalTax,
    totalAmount: s.totalAmount,
    createdByUserName,
    fiscalDocuments: [],
    returnStatus: 0,
    return: null,
    payments: s.payments ?? [],
    lines: s.lines.map((l) => ({
      id: l.id,
      productId: l.productId,
      productName: l.productName,
      productExtendedDataJson: l.productExtendedDataJson ?? '{}',
      quantity: l.quantity,
      lineNetSubtotal: l.lineNetSubtotal,
      lineTaxAmount: l.lineTaxAmount,
      lineTotal: l.lineNetSubtotal + l.lineTaxAmount,
      stockLotId: l.stockLotId ?? null,
      lotNumber: l.lotNumber ?? null
    }))
  };
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

@Injectable({ providedIn: 'root' })
export class SaleService {
  private readonly http = inject(HttpClient);
  private readonly productService = inject(ProductService);

  private readonly apiBaseUrl = '/api/sales';

  /** Estado de operación (evita suscripciones sueltas: la UI usa this.read() o señal local). */
  readonly saving = signal(false);

  create(payload: CreateSaleDto): Promise<RequestResult<SaleResponse>> {
    this.saving.set(true);
    return firstValueFrom(
      this.http.post<ApiResponse<SaleResponse>>(this.apiBaseUrl, payload).pipe(
        map((response) => {
          if (!response.success || response.data === null) {
            return this.toFailureResult<SaleResponse>(400, response.error);
          }
          return {
            success: true,
            data: response.data,
            error: null,
            status: 201
          } as RequestResult<SaleResponse>;
        }),
        finalize(() => this.saving.set(false))
      )
    ).catch((error: unknown) => this.toHttpErrorResult<SaleResponse>(error));
  }

  createReturn(saleId: string): Promise<RequestResult<SaleReturnView>> {
    this.saving.set(true);
    return firstValueFrom(
      this.http
        .post<ApiResponse<SaleReturnView>>(`${this.apiBaseUrl}/${saleId}/returns`, {})
        .pipe(
          map((response) => {
            if (!response.success || response.data === null) {
              return this.toFailureResult<SaleReturnView>(400, response.error);
            }
            return {
              success: true,
              data: response.data,
              error: null,
              status: 201
            } as RequestResult<SaleReturnView>;
          }),
          finalize(() => this.saving.set(false))
        )
    ).catch((error: unknown) => this.toHttpErrorResult<SaleReturnView>(error));
  }

  getSalesList(filters: {
    startDate?: string;
    endDate?: string;
    pageNumber: number;
    pageSize: number;
  }) {
    let params = new HttpParams()
      .set('pageNumber', String(filters.pageNumber))
      .set('pageSize', String(filters.pageSize));
    if (filters.startDate) {
      params = params.set('startDate', filters.startDate);
    }
    if (filters.endDate) {
      params = params.set('endDate', filters.endDate);
    }
    return this.http
      .get<ApiResponse<PagedSalesResult>>(this.apiBaseUrl, { params })
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  getById(id: string) {
    return this.http
      .get<ApiResponse<SaleDetailView>>(`${this.apiBaseUrl}/${id}`)
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  getDailySummary(date?: string) {
    let params = new HttpParams();
    if (date) {
      params = params.set('date', date);
    }
    return this.http
      .get<ApiResponse<DailySummaryResult>>(`${this.apiBaseUrl}/daily-summary`, { params })
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  getSalesReport(filters?: { startDate?: string; endDate?: string }) {
    let params = new HttpParams();
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate);
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate);
    }
    return this.http
      .get<ApiResponse<SalesReportResult>>(`${this.apiBaseUrl}/report`, { params })
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  getMarginReport(filters?: { startDate?: string; endDate?: string }) {
    let params = new HttpParams();
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate);
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate);
    }
    return this.http
      .get<ApiResponse<MarginReportResult>>(`${this.apiBaseUrl}/report/margin`, { params })
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  getTopSkusReport(filters?: {
    startDate?: string;
    endDate?: string;
    sortBy?: TopSkusSortBy;
    take?: number;
  }) {
    let params = new HttpParams();
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate);
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate);
    }
    if (filters?.sortBy) {
      params = params.set('sortBy', filters.sortBy);
    }
    if (filters?.take != null) {
      params = params.set('take', String(filters.take));
    }
    return this.http
      .get<ApiResponse<TopSkusReportResult>>(`${this.apiBaseUrl}/report/top-skus`, { params })
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  getSalesByPeriodReport(filters?: {
    startDate?: string;
    endDate?: string;
    period?: SalesPeriod;
  }) {
    let params = new HttpParams();
    if (filters?.startDate) {
      params = params.set('startDate', filters.startDate);
    }
    if (filters?.endDate) {
      params = params.set('endDate', filters.endDate);
    }
    if (filters?.period) {
      params = params.set('period', filters.period);
    }
    return this.http
      .get<ApiResponse<SalesByPeriodReportResult>>(`${this.apiBaseUrl}/report/by-period`, { params })
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  private requireSuccessData<T>(response: ApiResponse<T>): T {
    if (!response.success || response.data === null) {
      throw new Error(response.error?.message ?? 'No se pudo cargar la información de ventas.');
    }
    return response.data;
  }

  /** Tras vender, refresca el catálogo (stock) sin suscripción de larga duración. */
  async createAndRefreshProducts(payload: CreateSaleDto): Promise<RequestResult<SaleResponse>> {
    const result = await this.create(payload);
    if (result.success) {
      try {
        await firstValueFrom(this.productService.getAll());
      } catch {
        // El stock local puede desincronizarse; la venta ya se registró.
      }
    }
    return result;
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
      return {
        success: false,
        data: null,
        error: { code: 'request.error', message: 'Error inesperado.' },
        status: 500,
        rawError: null
      };
    }
    const body = error.error as { error?: ApiErrorBody; message?: string } | null;
    const apiError = body?.error ?? null;
    const fallbackMessage = body?.message ?? error.message ?? 'No se pudo completar la operación.';

    return {
      success: false,
      data: null,
      error: apiError ?? { code: 'request.error', message: fallbackMessage },
      status: error.status || 500,
      rawError: error.error
    };
  }
}
