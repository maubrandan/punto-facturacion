import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import type { TenantFiscalProfileView, UpsertTenantFiscalProfileDto } from '../models/fiscal.model';

export interface PlatformTenantSummary {
  id: string;
  name: string;
  contactEmail: string | null;
  status: 'Active' | 'Suspended' | 'Closed';
  createdAt: string;
}

export interface PlatformTenantDetail extends PlatformTenantSummary {
  updatedAt: string | null;
  suspendedAt: string | null;
  closedAt: string | null;
}

export interface TenantEntitlements {
  maxProducts: number | null;
  maxTenantUsers: number | null;
  salesEnabled: boolean;
}

export interface PlatformTenantUser {
  id: string;
  email: string;
  fullName: string;
  emailConfirmed: boolean;
  lockoutEnabled: boolean;
  lockoutEnd: string | null;
  blockedByPlatform: boolean;
}

export interface PlatformTenantUserPage {
  items: PlatformTenantUser[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PlatformTenantPage {
  items: PlatformTenantSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PlatformAuditEvent {
  id: number;
  createdAtUtc: string;
  action: string;
  actorUserId: string | null;
  actorEmail: string | null;
  resourceType: string | null;
  resourceId: string | null;
  affectedTenantId: string | null;
  details: string | null;
  justification: string | null;
  correlationId: string | null;
  ipAddress: string | null;
  isImpersonationContext: boolean;
}

export interface PlatformAuditPage {
  items: PlatformAuditEvent[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PlatformMutationAck {
  message: string;
}

export interface PlatformMetricsOverview {
  totalTenants: number;
  activeTenants: number;
  suspendedTenants: number;
  closedTenants: number;
  blockedTenantUsers: number;
  recentAuditEvents: number;
}

interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: { code: string; message: string } | null;
}

@Injectable({ providedIn: 'root' })
export class PlatformConsoleService {
  private readonly apiBase = '/api/platform';

  constructor(private readonly http: HttpClient) {}

  getTenants(page = 1, pageSize = 20, nameContains?: string): Observable<PlatformTenantPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (nameContains?.trim()) {
      params = params.set('nameContains', nameContains.trim());
    }
    return this.http
      .get<ApiResponse<PlatformTenantPage>>(`${this.apiBase}/tenants`, { params })
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo listar tenants.')));
  }

  getTenantById(tenantId: string): Observable<PlatformTenantDetail> {
    return this.http
      .get<ApiResponse<PlatformTenantDetail>>(`${this.apiBase}/tenants/${tenantId}`)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo cargar el tenant.')));
  }

  suspendTenant(tenantId: string): Observable<PlatformTenantDetail> {
    return this.http
      .post<ApiResponse<PlatformTenantDetail>>(`${this.apiBase}/tenants/${tenantId}/suspend`, {})
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo suspender el tenant.')));
  }

  closeTenant(tenantId: string): Observable<PlatformTenantDetail> {
    return this.http
      .post<ApiResponse<PlatformTenantDetail>>(`${this.apiBase}/tenants/${tenantId}/close`, {})
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo cerrar el tenant.')));
  }

  getEntitlements(tenantId: string): Observable<TenantEntitlements> {
    return this.http
      .get<ApiResponse<TenantEntitlements>>(`${this.apiBase}/tenants/${tenantId}/entitlements`)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudieron cargar entitlements.')));
  }

  setEntitlements(
    tenantId: string,
    payload: TenantEntitlements & { justification: string }
  ): Observable<TenantEntitlements> {
    return this.http
      .put<ApiResponse<TenantEntitlements>>(`${this.apiBase}/tenants/${tenantId}/entitlements`, payload)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudieron actualizar entitlements.')));
  }

  getFiscalProfile(tenantId: string): Observable<TenantFiscalProfileView | null> {
    return this.http
      .get<ApiResponse<TenantFiscalProfileView>>(`${this.apiBase}/tenants/${tenantId}/fiscal-profile`)
      .pipe(
        map((r) => (r.success && r.data ? r.data : null)),
        catchError(() => of(null))
      );
  }

  setFiscalProfile(
    tenantId: string,
    payload: UpsertTenantFiscalProfileDto & { justification: string }
  ): Observable<TenantFiscalProfileView> {
    return this.http
      .put<ApiResponse<TenantFiscalProfileView>>(`${this.apiBase}/tenants/${tenantId}/fiscal-profile`, payload)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo actualizar el perfil fiscal.')));
  }

  getTenantUsers(
    tenantId: string,
    page = 1,
    pageSize = 20,
    emailContains?: string
  ): Observable<PlatformTenantUserPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (emailContains?.trim()) {
      params = params.set('emailContains', emailContains.trim());
    }
    return this.http
      .get<ApiResponse<PlatformTenantUserPage>>(`${this.apiBase}/tenants/${tenantId}/users`, { params })
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudieron listar usuarios del tenant.')));
  }

  blockTenantUser(tenantId: string, userId: string, justification: string): Observable<PlatformTenantUser> {
    return this.http
      .post<ApiResponse<PlatformTenantUser>>(`${this.apiBase}/tenants/${tenantId}/users/${userId}/block`, { justification })
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo bloquear el usuario.')));
  }

  unblockTenantUser(tenantId: string, userId: string, justification: string): Observable<PlatformTenantUser> {
    return this.http
      .post<ApiResponse<PlatformTenantUser>>(`${this.apiBase}/tenants/${tenantId}/users/${userId}/unblock`, { justification })
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo desbloquear el usuario.')));
  }

  requestTenantUserPasswordReset(
    tenantId: string,
    userId: string,
    justification: string
  ): Observable<PlatformMutationAck> {
    return this.http
      .post<ApiResponse<PlatformMutationAck>>(
        `${this.apiBase}/tenants/${tenantId}/users/${userId}/request-password-reset`,
        { justification }
      )
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo solicitar reset de password.')));
  }

  resendTenantUserEmailConfirmation(
    tenantId: string,
    userId: string,
    justification: string
  ): Observable<PlatformMutationAck> {
    return this.http
      .post<ApiResponse<PlatformMutationAck>>(
        `${this.apiBase}/tenants/${tenantId}/users/${userId}/resend-email-confirmation`,
        { justification }
      )
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo reenviar la confirmación de email.')));
  }

  getAudit(page = 1, pageSize = 20, tenantId?: string): Observable<PlatformAuditPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (tenantId?.trim()) {
      params = params.set('tenantId', tenantId.trim());
    }
    return this.http
      .get<ApiResponse<PlatformAuditPage>>(`${this.apiBase}/audit`, { params })
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo listar auditoría.')));
  }

  getMetricsOverview(): Observable<PlatformMetricsOverview> {
    return this.http
      .get<ApiResponse<PlatformMetricsOverview>>(`${this.apiBase}/metrics/overview`)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudieron cargar métricas de plataforma.')));
  }

  private requireSuccessData<T>(response: ApiResponse<T>, fallback: string): T {
    if (!response.success || response.data === null) {
      throw new Error(response.error?.message ?? fallback);
    }
    return response.data;
  }
}
