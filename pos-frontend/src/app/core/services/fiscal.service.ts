import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { finalize, map } from 'rxjs/operators';
import type {
  FiscalDocumentView,
  IssueCreditNoteDto,
  IssueElectronicInvoiceDto,
  TenantFiscalProfileView,
  UpsertTenantFiscalProfileDto
} from '../models/fiscal.model';
import type { RequestResult } from './product.service';

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
export class FiscalService {
  private readonly http = inject(HttpClient);

  private readonly documentsBase = '/api/fiscal-documents';
  private readonly profileBase = '/api/fiscal/profile';

  readonly busy = signal(false);

  getProfile(): Promise<RequestResult<TenantFiscalProfileView | null>> {
    return firstValueFrom(
      this.http.get<ApiResponse<TenantFiscalProfileView>>(this.profileBase).pipe(
        map((response) => {
          if (response.success && response.data) {
            return { success: true, data: response.data, error: null, status: 200 } as const;
          }
          return this.toFailureResult<TenantFiscalProfileView | null>(404, response.error);
        })
      )
    ).catch((error: unknown) => this.toHttpErrorResult<TenantFiscalProfileView | null>(error));
  }

  saveProfile(payload: UpsertTenantFiscalProfileDto): Promise<RequestResult<TenantFiscalProfileView>> {
    this.busy.set(true);
    return firstValueFrom(
      this.http.put<ApiResponse<TenantFiscalProfileView>>(this.profileBase, payload).pipe(
        map((response) => {
          if (response.success && response.data) {
            return { success: true, data: response.data, error: null, status: 200 } as const;
          }
          return this.toFailureResult<TenantFiscalProfileView>(400, response.error);
        }),
        finalize(() => this.busy.set(false))
      )
    ).catch((error: unknown) => this.toHttpErrorResult<TenantFiscalProfileView>(error));
  }

  getBySale(saleId: string) {
    return this.http
      .get<ApiResponse<FiscalDocumentView[]>>(`${this.documentsBase}/by-sale/${saleId}`)
      .pipe(map((response) => this.requireSuccessData(response)));
  }

  issueInvoice(payload: IssueElectronicInvoiceDto): Promise<RequestResult<FiscalDocumentView>> {
    this.busy.set(true);
    return firstValueFrom(
      this.http.post<ApiResponse<FiscalDocumentView>>(`${this.documentsBase}/issue`, payload).pipe(
        map((response) => {
          if (response.success && response.data) {
            return { success: true, data: response.data, error: null, status: 200 } as const;
          }
          return this.toFailureResult<FiscalDocumentView>(400, response.error);
        }),
        finalize(() => this.busy.set(false))
      )
    ).catch((error: unknown) => this.toHttpErrorResult<FiscalDocumentView>(error));
  }

  retry(fiscalDocumentId: string): Promise<RequestResult<FiscalDocumentView>> {
    this.busy.set(true);
    return firstValueFrom(
      this.http
        .post<ApiResponse<FiscalDocumentView>>(`${this.documentsBase}/retry`, { fiscalDocumentId })
        .pipe(
          map((response) => {
            if (response.success && response.data) {
              return { success: true, data: response.data, error: null, status: 200 } as const;
            }
            return this.toFailureResult<FiscalDocumentView>(400, response.error);
          }),
          finalize(() => this.busy.set(false))
        )
    ).catch((error: unknown) => this.toHttpErrorResult<FiscalDocumentView>(error));
  }

  issueCreditNote(payload: IssueCreditNoteDto): Promise<RequestResult<FiscalDocumentView>> {
    this.busy.set(true);
    return firstValueFrom(
      this.http
        .post<ApiResponse<FiscalDocumentView>>(`${this.documentsBase}/credit-note`, payload)
        .pipe(
          map((response) => {
            if (response.success && response.data) {
              return { success: true, data: response.data, error: null, status: 200 } as const;
            }
            return this.toFailureResult<FiscalDocumentView>(400, response.error);
          }),
          finalize(() => this.busy.set(false))
        )
    ).catch((error: unknown) => this.toHttpErrorResult<FiscalDocumentView>(error));
  }

  private requireSuccessData<T>(response: ApiResponse<T>): T {
    if (!response.success || response.data === null) {
      throw new Error(response.error?.message ?? 'No se pudo cargar información fiscal.');
    }
    return response.data;
  }

  private toFailureResult<T>(status: number, error: ApiErrorBody | null): RequestResult<T> {
    return {
      success: false,
      data: null,
      error: error ?? { code: 'request.error', message: 'No se pudo completar la operación.' },
      status
    };
  }

  private toHttpErrorResult<T>(error: unknown): RequestResult<T> {
    if (!(error instanceof HttpErrorResponse)) {
      return {
        success: false,
        data: null,
        error: { code: 'request.error', message: 'Error inesperado.' },
        status: 500
      };
    }
    const body = error.error as { error?: ApiErrorBody; message?: string } | null;
    const apiError = body?.error ?? null;
    return {
      success: false,
      data: null,
      error: apiError ?? { code: 'request.error', message: body?.message ?? error.message },
      status: error.status || 500,
      rawError: error.error
    };
  }
}
