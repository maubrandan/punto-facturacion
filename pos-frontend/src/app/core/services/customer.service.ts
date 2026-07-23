import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, catchError, Observable, of, switchMap } from 'rxjs';
import { Customer } from '../models/customer.model';

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

export interface CustomerWritePayload {
  name: string;
  taxId: string;
  email: string;
  phone: string;
  address: string;
}

export interface CustomerAccountMovement {
  id: string;
  type: number;
  amount: number;
  balanceAfter: number;
  saleId: string | null;
  notes: string | null;
  settlementMethod: number | null;
  cashSessionId: string | null;
  createdByUserId: string | null;
  createdAt: string;
}

export interface CustomerAccount {
  customerId: string;
  customerName: string;
  balance: number;
  recentMovements: CustomerAccountMovement[];
}

export interface RegisterCustomerAccountPaymentPayload {
  amount: number;
  method: number;
  notes?: string | null;
}

export interface RegisterCustomerAccountPaymentResult {
  movementId: string;
  amount: number;
  balanceAfter: number;
  settlementMethod: number;
  cashSessionId: string | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/customers';

  getAll(q?: string): Observable<readonly Customer[]> {
    let params = new HttpParams();
    if (q?.trim()) {
      params = params.set('q', q.trim());
    }
    return this.http
      .get<ApiResponse<readonly Customer[]>>(this.base, { params })
      .pipe(map((r) => this.requireSuccessData(r)));
  }

  getById(id: string): Observable<Customer> {
    return this.http
      .get<ApiResponse<Customer>>(`${this.base}/${id}`)
      .pipe(map((r) => this.requireSuccessData(r)));
  }

  getAccount(id: string): Observable<CustomerAccount> {
    return this.http
      .get<ApiResponse<CustomerAccount>>(`${this.base}/${id}/account`)
      .pipe(map((r) => this.requireSuccessData(r)));
  }

  getMovements(id: string): Observable<readonly CustomerAccountMovement[]> {
    return this.http
      .get<ApiResponse<readonly CustomerAccountMovement[]>>(`${this.base}/${id}/movements`)
      .pipe(map((r) => this.requireSuccessData(r)));
  }

  registerAccountPayment(
    id: string,
    payload: RegisterCustomerAccountPaymentPayload
  ): Observable<RequestResult<RegisterCustomerAccountPaymentResult>> {
    return this.http
      .post<ApiResponse<RegisterCustomerAccountPaymentResult>>(
        `${this.base}/${id}/account/payments`,
        payload
      )
      .pipe(
        switchMap((response) => {
          if (!response.success || response.data === null) {
            return of(this.toFailure<RegisterCustomerAccountPaymentResult>(400, response.error));
          }
          return of({
            success: true,
            data: response.data,
            error: null,
            status: 200
          });
        }),
        catchError((e) => of(this.toHttpError<RegisterCustomerAccountPaymentResult>(e)))
      );
  }

  create(payload: CustomerWritePayload): Observable<RequestResult<Customer>> {
    return this.http.post<ApiResponse<Customer>>(this.base, payload).pipe(
      switchMap((response) => {
        if (!response.success || response.data === null) {
          return of(this.toFailure<Customer>(400, response.error));
        }
        return of({
          success: true,
          data: response.data,
          error: null,
          status: 201
        });
      }),
      catchError((e) => of(this.toHttpError<Customer>(e)))
    );
  }

  update(id: string, payload: CustomerWritePayload): Observable<RequestResult<Customer>> {
    return this.http.put<ApiResponse<Customer>>(`${this.base}/${id}`, payload).pipe(
      switchMap((response) => {
        if (!response.success || response.data === null) {
          return of(this.toFailure<Customer>(400, response.error));
        }
        return of({
          success: true,
          data: response.data,
          error: null,
          status: 200
        });
      }),
      catchError((e) => of(this.toHttpError<Customer>(e)))
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
      throw new Error(response.error?.message ?? 'Error de clientes.');
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
