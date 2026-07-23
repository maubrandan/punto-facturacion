import { computed, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable, tap } from 'rxjs';
import {
  hasPlatformImpersonation,
  hasPlatformOperations,
  hasPlatformSuperAdmin,
  PLATFORM_ROLES,
  PlatformUser
} from '../models/platform-user.model';

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
export class PlatformAuthService {
  private readonly tokenStorageKey = 'platform_auth_token';
  private readonly apiBaseUrl = '/api/platform/auth';

  readonly currentUser = signal<PlatformUser | null>(null);
  readonly isAuthenticated = computed(() => !!this.currentUser());

  /** Roles del usuario de plataforma actual. */
  readonly roles = computed(() => this.currentUser()?.roles ?? []);

  /** Mutaciones de tenants / entitlements / fiscal / usuarios tenant (policy Platform.Operations). */
  readonly canOperate = computed(() => hasPlatformOperations(this.roles()));

  /** Iniciar sesión de soporte en un tenant (policy Platform.Impersonation). */
  readonly canImpersonate = computed(() => hasPlatformImpersonation(this.roles()));

  /** CRUD de operadores (policy Platform.SuperAdmin). */
  readonly canManageOperators = computed(() => hasPlatformSuperAdmin(this.roles()));

  readonly isSuperAdmin = computed(() => hasPlatformSuperAdmin(this.roles()));

  readonly isSupportReadOnly = computed(() =>
    this.roles().includes(PLATFORM_ROLES.SupportReadOnly) && !this.canOperate()
  );

  constructor(private readonly http: HttpClient) {
    this.restoreSession();
  }

  login(payload: LoginRequest): Observable<PlatformUser> {
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
    this.currentUser.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenStorageKey);
  }

  private restoreSession(): void {
    const token = this.getToken();
    if (!token) {
      return;
    }
    const user = this.extractUserFromToken(token);
    if (!user) {
      localStorage.removeItem(this.tokenStorageKey);
      return;
    }
    this.currentUser.set(user);
  }

  private setSession(token: string): void {
    localStorage.setItem(this.tokenStorageKey, token);
    this.currentUser.set(this.extractUserFromToken(token));
  }

  private requireSuccessData(response: ApiResponse<AuthResponse>): AuthResponse {
    if (!response.success || !response.data) {
      throw new Error(response.error?.message ?? 'No se pudo autenticar en plataforma.');
    }
    return response.data;
  }

  private toUser(auth: AuthResponse): PlatformUser {
    return {
      userId: auth.userId,
      email: auth.email,
      roles: this.extractRolesFromToken(auth.accessToken)
    };
  }

  private extractUserFromToken(token: string): PlatformUser | null {
    try {
      const payload = this.decodeJwtPayload(token);
      const isPlatform = this.readStringClaim(payload, 'is_platform');
      if (isPlatform?.toLowerCase() !== 'true') {
        return null;
      }

      const userId = this.readStringClaim(payload, 'sub') ?? this.readStringClaim(payload, 'nameid');
      const email = this.readStringClaim(payload, 'email');
      if (!userId || !email) {
        return null;
      }

      return {
        userId,
        email,
        roles: this.extractRoles(payload)
      };
    } catch {
      return null;
    }
  }

  private extractRolesFromToken(token: string): string[] {
    try {
      return this.extractRoles(this.decodeJwtPayload(token));
    } catch {
      return [];
    }
  }

  private extractRoles(payload: Record<string, unknown>): string[] {
    const roleClaim = payload['role'] ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    if (Array.isArray(roleClaim)) {
      return roleClaim.filter((x): x is string => typeof x === 'string' && x.length > 0);
    }
    if (typeof roleClaim === 'string' && roleClaim.length > 0) {
      return [roleClaim];
    }
    return [];
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
}
