export interface PurchaseLine {
  id: string;
  productId: string;
  productName: string;
  productSku: string;
  quantity: number;
  unitCost: number;
  subtotal: number;
}

export interface Purchase {
  id: string;
  providerId: string;
  providerName: string;
  date: string;
  invoiceNumber: string;
  total: number;
  lines: readonly PurchaseLine[];
}

export interface PurchaseSummary {
  id: string;
  providerId: string;
  providerName: string;
  date: string;
  invoiceNumber: string;
  total: number;
}

export interface CreatePurchaseLineDto {
  productId: string;
  quantity: number;
  unitCost: number;
}

export interface CreatePurchaseDto {
  providerId: string;
  date: string;
  invoiceNumber: string;
  lines: CreatePurchaseLineDto[];
}
