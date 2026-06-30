import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { finalize, map } from 'rxjs/operators';
import type { FiscalDocumentView } from '../models/fiscal.model';
import { ProductService, RequestResult } from './product.service';

export interface CreateSaleLineDto {
  productId: string;
  quantity: number;
}

export interface CreateSaleDto {
  lines: CreateSaleLineDto[];
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
}

export interface SaleDetailView {
  id: string;
  date: string;
  totalNet: number;
  totalTax: number;
  totalAmount: number;
  createdByUserName: string | null;
  lines: SaleDetailLineView[];
  fiscalDocuments?: FiscalDocumentView[];
}

export interface DailySummaryResult {
  totalFacturado: number;
  ventasCount: number;
  topProductId: string | null;
  topProductName: string | null;
  topProductUnits: number;
}

export interface SaleResponse {
  id: string;
  date: string;
  totalNet: number;
  totalTax: number;
  totalAmount: number;
  lines: SaleLineResponse[];
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
    lines: s.lines.map((l) => ({
      id: l.id,
      productId: l.productId,
      productName: l.productName,
      productExtendedDataJson: l.productExtendedDataJson ?? '{}',
      quantity: l.quantity,
      lineNetSubtotal: l.lineNetSubtotal,
      lineTaxAmount: l.lineTaxAmount,
      lineTotal: l.lineNetSubtotal + l.lineTaxAmount
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
