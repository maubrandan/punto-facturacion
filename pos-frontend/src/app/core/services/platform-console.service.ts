import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import type { TenantFiscalProfileView, UpsertTenantFiscalProfileDto } from '../models/fiscal.model';

export type PlatformTenantStatus = 'Active' | 'Suspended' | 'Closed';

export interface PlatformTenantSummary {
  id: string;
  name: string;
  contactEmail: string | null;
  status: PlatformTenantStatus;
  createdAt: string;
}

export interface PlatformTenantDetail extends PlatformTenantSummary {
  businessType: string;
  updatedAt: string | null;
  suspendedAt: string | null;
  closedAt: string | null;
}

export type TenantPlanCode = 'Starter' | 'Pro' | 'Unlimited';

export interface CreatePlatformTenantPayload {
  name: string;
  contactEmail?: string | null;
  businessType: string;
  adminEmail: string;
  adminFullName?: string | null;
  adminPassword: string;
  planCode?: TenantPlanCode | string | null;
  maxProducts?: number | null;
  maxTenantUsers?: number | null;
  salesEnabled?: boolean | null;
}

export interface UpdatePlatformTenantPayload {
  name: string;
  contactEmail?: string | null;
}

export interface TenantEntitlements {
  maxProducts: number | null;
  maxTenantUsers: number | null;
  salesEnabled: boolean;
  /** Preset coincidente (Starter/Pro/Unlimited) o null si es custom. */
  matchedPlanCode?: string | null;
}

/** Valores numéricos del enum backend (System.Text.Json por defecto). */
export type SubscriptionStatusCode = 0 | 1 | 2 | 3;
export type BillingCycleCode = 0 | 1;

export interface TenantSubscription {
  tenantId: string;
  planCode: TenantPlanCode | string;
  matchedPlanCode?: string | null;
  entitlementsMatchPlan: boolean;
  status: SubscriptionStatusCode | number;
  billingCycle: BillingCycleCode | number;
  provider: number;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  trialEndsAtUtc: string | null;
  cancelAtPeriodEnd: boolean;
  canceledAtUtc: string | null;
  notes: string | null;
  updatedAtUtc: string;
}

export interface UpdateTenantSubscriptionPayload {
  planCode: TenantPlanCode | string;
  status: SubscriptionStatusCode | number;
  billingCycle: BillingCycleCode | number;
  currentPeriodStartUtc?: string | null;
  currentPeriodEndUtc?: string | null;
  trialEndsAtUtc?: string | null;
  cancelAtPeriodEnd?: boolean;
  notes?: string | null;
  justification: string;
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

export interface ImpersonationSessionResponse {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  tenantId: string;
}

export type PlatformOperatorRole =
  | 'Platform.SuperAdmin'
  | 'Platform.Operations'
  | 'Platform.Support'
  | 'Platform.SupportReadOnly';

export interface PlatformOperator {
  id: string;
  email: string;
  fullName: string;
  platformRole: PlatformOperatorRole | string;
  emailConfirmed: boolean;
  blockedByPlatform: boolean;
}

export interface PlatformOperatorPage {
  items: PlatformOperator[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ProvisionPlatformOperatorPayload {
  email: string;
  password: string;
  fullName: string;
  platformRole: PlatformOperatorRole | string;
}

export interface UpdatePlatformOperatorPayload {
  fullName: string;
  platformRole: PlatformOperatorRole | string;
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

  getTenants(
    page = 1,
    pageSize = 20,
    nameContains?: string,
    status?: PlatformTenantStatus | ''
  ): Observable<PlatformTenantPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (nameContains?.trim()) {
      params = params.set('nameContains', nameContains.trim());
    }
    if (status) {
      params = params.set('status', status);
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

  createTenant(payload: CreatePlatformTenantPayload): Observable<PlatformTenantDetail> {
    return this.http
      .post<ApiResponse<PlatformTenantDetail>>(`${this.apiBase}/tenants`, payload)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo crear el tenant.')));
  }

  updateTenant(tenantId: string, payload: UpdatePlatformTenantPayload): Observable<PlatformTenantDetail> {
    return this.http
      .patch<ApiResponse<PlatformTenantDetail>>(`${this.apiBase}/tenants/${tenantId}`, payload)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo actualizar el tenant.')));
  }

  suspendTenant(tenantId: string): Observable<PlatformTenantDetail> {
    return this.http
      .post<ApiResponse<PlatformTenantDetail>>(`${this.apiBase}/tenants/${tenantId}/suspend`, {})
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo suspender el tenant.')));
  }

  unsuspendTenant(tenantId: string): Observable<PlatformTenantDetail> {
    return this.http
      .post<ApiResponse<PlatformTenantDetail>>(`${this.apiBase}/tenants/${tenantId}/unsuspend`, {})
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo reactivar el tenant.')));
  }

  closeTenant(tenantId: string): Observable<PlatformTenantDetail> {
    return this.http
      .post<ApiResponse<PlatformTenantDetail>>(`${this.apiBase}/tenants/${tenantId}/close`, {})
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo cerrar el tenant.')));
  }

  reopenTenant(tenantId: string, justification: string): Observable<PlatformTenantDetail> {
    return this.http
      .post<ApiResponse<PlatformTenantDetail>>(`${this.apiBase}/tenants/${tenantId}/reopen`, { justification })
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo reabrir el tenant.')));
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

  getSubscription(tenantId: string): Observable<TenantSubscription> {
    return this.http
      .get<ApiResponse<TenantSubscription>>(`${this.apiBase}/tenants/${tenantId}/subscription`)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo cargar la suscripción.')));
  }

  updateSubscription(
    tenantId: string,
    payload: UpdateTenantSubscriptionPayload
  ): Observable<TenantSubscription> {
    return this.http
      .put<ApiResponse<TenantSubscription>>(`${this.apiBase}/tenants/${tenantId}/subscription`, payload)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo actualizar la suscripción.')));
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

  getOperators(
    page = 1,
    pageSize = 20,
    emailContains?: string,
    role?: string
  ): Observable<PlatformOperatorPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (emailContains?.trim()) {
      params = params.set('emailContains', emailContains.trim());
    }
    if (role?.trim()) {
      params = params.set('role', role.trim());
    }
    return this.http
      .get<ApiResponse<PlatformOperatorPage>>(`${this.apiBase}/operators`, { params })
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudieron listar operadores.')));
  }

  provisionOperator(payload: ProvisionPlatformOperatorPayload): Observable<PlatformOperator> {
    return this.http
      .post<ApiResponse<PlatformOperator>>(`${this.apiBase}/operators`, payload)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo aprovisionar el operador.')));
  }

  updateOperator(userId: string, payload: UpdatePlatformOperatorPayload): Observable<PlatformOperator> {
    return this.http
      .patch<ApiResponse<PlatformOperator>>(`${this.apiBase}/operators/${userId}`, payload)
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo actualizar el operador.')));
  }

  blockOperator(userId: string, justification: string): Observable<PlatformOperator> {
    return this.http
      .post<ApiResponse<PlatformOperator>>(`${this.apiBase}/operators/${userId}/block`, { justification })
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo bloquear el operador.')));
  }

  unblockOperator(userId: string, justification: string): Observable<PlatformOperator> {
    return this.http
      .post<ApiResponse<PlatformOperator>>(`${this.apiBase}/operators/${userId}/unblock`, { justification })
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo desbloquear el operador.')));
  }

  startImpersonation(payload: {
    tenantId: string;
    reason: string;
    ttlMinutes?: number;
  }): Observable<ImpersonationSessionResponse> {
    return this.http
      .post<ApiResponse<ImpersonationSessionResponse>>(
        `${this.apiBase}/support/impersonation/session`,
        {
          tenantId: payload.tenantId,
          reason: payload.reason,
          ttlMinutes: payload.ttlMinutes ?? 15
        }
      )
      .pipe(map((r) => this.requireSuccessData(r, 'No se pudo iniciar la sesión de soporte.')));
  }

  private requireSuccessData<T>(response: ApiResponse<T>, fallback: string): T {
    if (!response.success || response.data === null) {
      throw new Error(response.error?.message ?? fallback);
    }
    return response.data;
  }
}
