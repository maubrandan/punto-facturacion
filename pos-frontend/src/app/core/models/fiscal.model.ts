export type FiscalDocumentType = 1 | 2 | 3 | 4;

export type FiscalDocumentStatus = 0 | 1 | 2 | 3 | 4 | 5;

export interface FiscalDocumentView {
  id: string;
  saleId: string;
  originalFiscalDocumentId: string | null;
  documentType: FiscalDocumentType;
  status: FiscalDocumentStatus;
  pointOfSale: number;
  voucherNumber: number | null;
  cae: string | null;
  caeExpiresAtUtc: string | null;
  lastErrorCode: string | null;
  lastErrorMessage: string | null;
  retryCount: number;
  nextRetryAtUtc: string | null;
  correlationId: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  buyerTaxId: string | null;
  buyerName: string | null;
  authorizedAmount: number | null;
  documentTypeLabel: string | null;
  afipQrUrl: string | null;
}

export interface TenantFiscalProfileView {
  id: string;
  taxId: string;
  pointOfSale: number;
  isProduction: boolean;
  isEnabled: boolean;
  certificateRef: string;
  privateKeyRef: string;
  updatedAtUtc: string;
}

export interface UpsertTenantFiscalProfileDto {
  taxId: string;
  pointOfSale: number;
  isProduction: boolean;
  isEnabled: boolean;
  certificateRef: string;
  privateKeyRef: string;
}

export interface IssueElectronicInvoiceDto {
  saleId: string;
  isInvoiceA: boolean;
  buyerTaxId?: string | null;
  buyerName?: string | null;
  customerId?: string | null;
}

export interface IssueCreditNoteDto {
  originalFiscalDocumentId: string;
  saleId: string;
  amount: number;
}

export function fiscalStatusLabel(status: FiscalDocumentStatus): string {
  switch (status) {
    case 0:
      return 'Borrador';
    case 1:
      return 'Pendiente ARCA';
    case 2:
      return 'Autorizado';
    case 3:
      return 'Rechazado';
    case 4:
      return 'Reintento programado';
    case 5:
      return 'Anulado';
    default:
      return 'Desconocido';
  }
}

export function isFiscalAuthorized(doc: FiscalDocumentView): boolean {
  return doc.status === 2 && !!doc.cae;
}

export function formatVoucher(doc: FiscalDocumentView): string {
  if (doc.voucherNumber == null) {
    return '—';
  }
  const padded = String(doc.pointOfSale).padStart(5, '0');
  return `${padded}-${String(doc.voucherNumber).padStart(8, '0')}`;
}
