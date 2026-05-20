import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable, tap } from 'rxjs';
import { UI_DENSITY_LEGACY_STORAGE_KEY } from '../constants/ui-density-storage';
import { ImpersonationSessionInfo } from '../models/impersonation-session.model';
import { User } from '../models/user.model';

interface RegisterRequest {
  email: string;
  password: string;
  businessName: string;
  fullName?: string;
}

interface LoginRequest {
  email: string;
  password: string;
}

interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  userId: string;
  email: string;
  tenantId: string;
  businessType?: string;
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
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly apiBaseUrl = '/api/auth';
  private readonly tokenStorageKey = 'auth_token';
  private readonly defaultBusinessType: User['businessType'] = 'kiosco';

  readonly currentUser = signal<User | null>(null);
  readonly isAuthenticated = computed(() => !!this.currentUser());

  private readonly impersonationSessionWritable = signal<ImpersonationSessionInfo | null>(null);
  /** Presente cuando el access token actual es sesión de soporte en contexto tenant. */
  readonly impersonationSession = this.impersonationSessionWritable.asReadonly();

  constructor() {
    this.restoreSession();
  }

  register(payload: RegisterRequest): Observable<User> {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${this.apiBaseUrl}/register`, payload)
      .pipe(
        map((response) => this.requireSuccessData(response)),
        tap((auth) => this.setSession(auth.accessToken)),
        map((auth) => this.toUser(auth))
      );
  }

  login(payload: LoginRequest): Observable<User> {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${this.apiBaseUrl}/login`, payload)
      .pipe(
        map((response) => this.requireSuccessData(response)),
        tap((auth) => this.setSession(auth.accessToken)),
        map((auth) => this.toUser(auth))
      );
  }

  logout(): void {
    localStorage.removeItem(this.tokenStorageKey);
    /** Evita que otro usuario en el mismo navegador migre/importe la densidad global antigua */
    localStorage.removeItem(UI_DENSITY_LEGACY_STORAGE_KEY);
    this.currentUser.set(null);
    this.impersonationSessionWritable.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenStorageKey);
  }

  private restoreSession(): void {
    const token = this.getToken();
    if (!token) {
      this.impersonationSessionWritable.set(null);
      return;
    }

    const user = this.extractUserFromToken(token);
    if (!user) {
      localStorage.removeItem(this.tokenStorageKey);
      this.impersonationSessionWritable.set(null);
      return;
    }

    this.currentUser.set(user);
    this.syncImpersonationFromStoredToken();
  }

  private setSession(token: string): void {
    localStorage.setItem(this.tokenStorageKey, token);
    const user = this.extractUserFromToken(token);
    this.currentUser.set(user);
    this.syncImpersonationFromStoredToken();
  }

  private syncImpersonationFromStoredToken(): void {
    this.impersonationSessionWritable.set(this.parseImpersonationFromToken(this.getToken()));
  }

  private parseImpersonationFromToken(token: string | null): ImpersonationSessionInfo | null {
    if (!token) {
      return null;
    }
    try {
      const payload = this.decodeJwtPayload(token);
      const flag = this.readStringClaim(payload, 'impersonation');
      if (flag !== 'true' && flag !== '1') {
        return null;
      }
      const reason = this.readStringClaim(payload, 'imp_reason')?.trim() || 'No indicado';
      return { reason };
    } catch {
      return null;
    }
  }

  private requireSuccessData(response: ApiResponse<AuthResponse>): AuthResponse {
    if (!response.success || !response.data) {
      throw new Error(response.error?.message ?? 'No se pudo autenticar.');
    }

    return response.data;
  }

  private extractUserFromToken(token: string): User | null {
    try {
      const payload = this.decodeJwtPayload(token);
      const userId = this.readStringClaim(payload, 'sub') ?? this.readStringClaim(payload, 'nameid');
      const email = this.readStringClaim(payload, 'email');
      const tenantId = this.readStringClaim(payload, 'tenant_id');
      const businessTypeRaw =
        this.readStringClaim(payload, 'business_type')
        ?? this.readStringClaim(payload, 'rubro')
        ?? this.readStringClaim(payload, 'businessType');

      if (!userId || !email || !tenantId) {
        return null;
      }

      return {
        userId,
        email,
        tenantId,
        businessType: this.normalizeBusinessType(businessTypeRaw)
      };
    } catch {
      return null;
    }
  }

  private toUser(auth: AuthResponse): User {
    return {
      userId: auth.userId,
      email: auth.email,
      tenantId: auth.tenantId,
      businessType: this.normalizeBusinessType(auth.businessType ?? null)
    };
  }

  private decodeJwtPayload(token: string): Record<string, unknown> {
    const segments = token.split('.');
    if (segments.length !== 3) {
      throw new Error('JWT inválido.');
    }

    const base64 = segments[1].replace(/-/g, '+').replace(/_/g, '/');
    const padding = '='.repeat((4 - (base64.length % 4)) % 4);
    const json = atob(base64 + padding);
    return JSON.parse(json) as Record<string, unknown>;
  }

  private readStringClaim(payload: Record<string, unknown>, claim: string): string | null {
    const value = payload[claim];
    return typeof value === 'string' && value.length > 0 ? value : null;
  }

  private normalizeBusinessType(value: string | null): User['businessType'] {
    const normalized = value?.trim().toLowerCase();
    if (normalized === 'farmacia' || normalized === 'ferreteria' || normalized === 'kiosco') {
      return normalized;
    }

    return this.defaultBusinessType;
  }
}
