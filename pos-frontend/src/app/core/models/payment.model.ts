/** Códigos alineados con POS.Domain.Entities.PaymentMethod. */
export const PAYMENT_METHOD = {
  Cash: 0,
  Card: 1,
  Transfer: 2
} as const;

export type PaymentMethodCode = (typeof PAYMENT_METHOD)[keyof typeof PAYMENT_METHOD];

export function paymentMethodLabel(method: number): string {
  switch (method) {
    case PAYMENT_METHOD.Cash:
      return 'Efectivo';
    case PAYMENT_METHOD.Card:
      return 'Tarjeta';
    case PAYMENT_METHOD.Transfer:
      return 'Transferencia';
    default:
      return `Medio ${method}`;
  }
}
