import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import type {
  BillingCycleCode,
  TenantPlanCode,
  TenantSubscription
} from './platform-console.service';

interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: { code: string; message: string } | null;
}

export interface SubscriptionInvoice {
  id: string;
  tenantId: string;
  invoiceNumber: string;
  status: number;
  planCode: string;
  billingCycle: BillingCycleCode | number;
  periodStartUtc: string;
  periodEndUtc: string;
  amount: number;
  currency: string;
  provider: number;
  externalInvoiceId: string | null;
  dueAtUtc: string;
  paidAtUtc: string | null;
  notes: string | null;
  createdAtUtc: string;
}

export interface SubscriptionInvoiceList {
  items: SubscriptionInvoice[];
  totalCount: number;
}

export interface SelfServeUpgradeResult {
  subscription: TenantSubscription;
  invoice: SubscriptionInvoice | null;
  appliedImmediately: boolean;
  checkoutUrl: string | null;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class TenantSubscriptionService {
  constructor(private readonly http: HttpClient) {}

  getMine(): Observable<TenantSubscription> {
    return this.http.get<ApiResponse<TenantSubscription>>('/api/tenant/subscription').pipe(
      map((r) => {
        if (!r.success || r.data === null) {
          throw new Error(r.error?.message ?? 'No se pudo cargar el plan del negocio.');
        }
        return r.data;
      })
    );
  }

  upgrade(planCode: TenantPlanCode | string, billingCycle: BillingCycleCode | number): Observable<SelfServeUpgradeResult> {
    return this.http
      .post<ApiResponse<SelfServeUpgradeResult>>('/api/tenant/subscription/upgrade', {
        planCode,
        billingCycle
      })
      .pipe(
        map((r) => {
          if (!r.success || r.data === null) {
            throw new Error(r.error?.message ?? 'No se pudo actualizar el plan.');
          }
          return r.data;
        })
      );
  }

  listInvoices(page = 1, pageSize = 20): Observable<SubscriptionInvoiceList> {
    return this.http
      .get<ApiResponse<SubscriptionInvoiceList>>('/api/tenant/invoices', {
        params: { page, pageSize }
      })
      .pipe(
        map((r) => {
          if (!r.success || r.data === null) {
            throw new Error(r.error?.message ?? 'No se pudieron cargar las facturas.');
          }
          return r.data;
        })
      );
  }
}
