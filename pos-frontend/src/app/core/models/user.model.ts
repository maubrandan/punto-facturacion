export interface User {
  userId: string;
  email: string;
  tenantId: string;
  businessType: 'farmacia' | 'ferreteria' | 'kiosco';
}
