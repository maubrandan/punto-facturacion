export interface CashSessionSummary {
  sessionId: string | null;
  openingDate: string | null;
  initialAmount: number | null;
  totalSales: number;
  totalCashPayments: number;
  totalCardPayments: number;
  totalTransferPayments: number;
  totalPurchases: number;
  totalExpenses: number;
  projectedAmount: number;
}

export interface CashSessionOpen {
  id: string;
  openingDate: string;
  initialAmount: number;
  state: number;
  userId: string | null;
}

export interface CashSessionClose {
  id: string;
  initialAmount: number;
  expectedAmount: number;
  countedAmount: number;
  difference: number;
  closingDate: string | null;
}

export interface ExpenseCategory {
  id: string;
  name: string;
}

export interface RegisterExpenseBody {
  description: string;
  amount: number;
  categoryId: string;
  date?: string | null;
}
