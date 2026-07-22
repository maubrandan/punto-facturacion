import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import type { TenantRole } from '../models/user.model';

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

export interface TenantUserRow {
  id: string;
  email: string;
  fullName: string;
  role: TenantRole | string;
  emailConfirmed: boolean;
  blockedByTenant: boolean;
  blockedByPlatform: boolean;
  lockoutEnabled: boolean;
  lockoutEnd: string | null;
}

export interface TenantUserPage {
  items: TenantUserRow[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class TenantUsersService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/users';

  list(page = 1, pageSize = 50, emailContains?: string): Observable<TenantUserPage> {
    let params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    if (emailContains?.trim()) {
      params = params.set('emailContains', emailContains.trim());
    }
    return this.http
      .get<ApiResponse<TenantUserPage>>(this.base, { params })
      .pipe(map((r) => this.require(r)));
  }

  create(payload: {
    email: string;
    password: string;
    fullName: string;
    role: string;
  }): Observable<RequestResult<TenantUserRow>> {
    return this.http.post<ApiResponse<TenantUserRow>>(this.base, payload).pipe(
      map((r) => this.toResult(r, 201)),
      catchError((e: unknown) => of(this.httpError<TenantUserRow>(e)))
    );
  }

  update(
    userId: string,
    payload: { fullName: string; role: string }
  ): Observable<RequestResult<TenantUserRow>> {
    return this.http.put<ApiResponse<TenantUserRow>>(`${this.base}/${userId}`, payload).pipe(
      map((r) => this.toResult(r, 200)),
      catchError((e: unknown) => of(this.httpError<TenantUserRow>(e)))
    );
  }

  block(userId: string): Observable<RequestResult<TenantUserRow>> {
    return this.http.post<ApiResponse<TenantUserRow>>(`${this.base}/${userId}/block`, {}).pipe(
      map((r) => this.toResult(r, 200)),
      catchError((e: unknown) => of(this.httpError<TenantUserRow>(e)))
    );
  }

  unblock(userId: string): Observable<RequestResult<TenantUserRow>> {
    return this.http.post<ApiResponse<TenantUserRow>>(`${this.base}/${userId}/unblock`, {}).pipe(
      map((r) => this.toResult(r, 200)),
      catchError((e: unknown) => of(this.httpError<TenantUserRow>(e)))
    );
  }

  private require<T>(response: ApiResponse<T>): T {
    if (!response.success || response.data === null) {
      throw new Error(response.error?.message ?? 'Error de usuarios.');
    }
    return response.data;
  }

  private toResult<T>(response: ApiResponse<T>, okStatus: number): RequestResult<T> {
    if (!response.success || response.data === null) {
      return {
        success: false,
        data: null,
        error: response.error ?? { code: 'request.error', message: 'Operación fallida.' },
        status: 400
      };
    }
    return { success: true, data: response.data, error: null, status: okStatus };
  }

  private httpError<T>(error: unknown): RequestResult<T> {
    if (!(error instanceof HttpErrorResponse)) {
      return {
        success: false,
        data: null,
        error: { code: 'request.error', message: 'Error inesperado.' },
        status: 500
      };
    }
    const body = error.error as { error?: ApiErrorBody; message?: string } | null;
    return {
      success: false,
      data: null,
      error: body?.error ?? { code: 'request.error', message: body?.message ?? error.message },
      status: error.status || 500
    };
  }
}
