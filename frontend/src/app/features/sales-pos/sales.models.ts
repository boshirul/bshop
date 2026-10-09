import { ApiResponse } from '../../core/auth/auth.models';

export type { ApiResponse };

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type SaleStatus = 'Completed' | 'Cancelled';
export type SalePaymentStatus = 'Unpaid' | 'Partial' | 'Paid';

export interface SaleListItem {
  id: string;
  invoiceNumber: string;
  customerId: string | null;
  customerName: string;
  saleDate: string;
  grandTotal: number;
  paidAmount: number;
  dueAmount: number;
  status: SaleStatus;
  paymentStatus: SalePaymentStatus;
}

export interface SaleLine {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  unitSymbol: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  vatAmount: number;
  lineTotal: number;
}

export interface SalePayment {
  id: string;
  paymentMethodId: string;
  paymentMethodName: string;
  amount: number;
  paidOn: string;
  referenceNumber: string | null;
  notes: string | null;
}

export interface SaleDetail extends SaleListItem {
  customerPhone: string | null;
  subtotal: number;
  discountAmount: number;
  vatAmount: number;
  notes: string | null;
  profit: number;
  cancellationReason: string | null;
  cancelledOn: string | null;
  items: SaleLine[];
  payments: SalePayment[];
}

export interface CreateSaleRequest {
  customerId: string | null;
  saleDate: string;
  notes: string | null;
  items: Array<{
    productId: string;
    quantity: number;
    unitPrice: number;
    discountAmount: number;
    vatAmount: number;
    serialNumbers?: string[];
  }>;
  payments: Array<{
    paymentMethodId: string;
    amount: number;
    referenceNumber: string | null;
    notes: string | null;
  }>;
}

export interface RecordSalePaymentRequest {
  paymentMethodId: string;
  amount: number;
  paidOn: string;
  referenceNumber: string | null;
  notes: string | null;
}

export type CustomerLedgerEntryType = 'Sale' | 'Payment' | 'Cancellation' | 'Refund';

export interface CustomerLedgerEntry {
  id: string;
  entryDate: string;
  entryType: CustomerLedgerEntryType;
  debit: number;
  credit: number;
  balance: number;
  referenceType: string;
  referenceId: string;
  referenceNumber: string;
  notes: string | null;
}

export interface CustomerLedger {
  customerId: string;
  customerCode: string;
  customerName: string;
  currentBalance: number;
  creditLimit: number;
  entries: PagedResult<CustomerLedgerEntry>;
}
