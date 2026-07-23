export type PlatformRole =
  | 'Platform.SuperAdmin'
  | 'Platform.Operations'
  | 'Platform.Support'
  | 'Platform.SupportReadOnly';

export interface PlatformUser {
  userId: string;
  email: string;
  roles: string[];
}

export const PLATFORM_ROLES = {
  SuperAdmin: 'Platform.SuperAdmin' as PlatformRole,
  Operations: 'Platform.Operations' as PlatformRole,
  Support: 'Platform.Support' as PlatformRole,
  SupportReadOnly: 'Platform.SupportReadOnly' as PlatformRole
} as const;

export const PLATFORM_ROLE_LABELS: Record<PlatformRole, string> = {
  'Platform.SuperAdmin': 'SuperAdmin',
  'Platform.Operations': 'Operations',
  'Platform.Support': 'Support',
  'Platform.SupportReadOnly': 'Support (solo lectura)'
};

/** Alineado a policy Platform.Operations. */
export function hasPlatformOperations(roles: readonly string[]): boolean {
  return roles.includes(PLATFORM_ROLES.Operations) || roles.includes(PLATFORM_ROLES.SuperAdmin);
}

/** Alineado a policy Platform.Impersonation. */
export function hasPlatformImpersonation(roles: readonly string[]): boolean {
  return (
    roles.includes(PLATFORM_ROLES.Support) ||
    roles.includes(PLATFORM_ROLES.SupportReadOnly) ||
    roles.includes(PLATFORM_ROLES.Operations) ||
    roles.includes(PLATFORM_ROLES.SuperAdmin)
  );
}

/** Alineado a policy Platform.SuperAdmin. */
export function hasPlatformSuperAdmin(roles: readonly string[]): boolean {
  return roles.includes(PLATFORM_ROLES.SuperAdmin);
}
