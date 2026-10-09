import { ApiResponse } from '../../core/auth/auth.models';

export type { ApiResponse };

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type PurchaseStatus = 'Draft' | 'Confirmed' | 'PartiallyReturned' | 'Returned' | 'Cancelled';
export type PurchasePaymentStatus = 'Unpaid' | 'Partial' | 'Paid' | 'Credit';

export interface PurchaseListItem {
  id: string;
  purchaseNumber: string;
  supplierId: string;
  supplierName: string;
  supplierInvoiceNumber: string | null;
  purchaseDate: string;
  grandTotal: number;
  paidAmount: number;
  dueAmount: number;
  status: PurchaseStatus;
  paymentStatus: PurchasePaymentStatus;
}

export interface PurchaseLine {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  unitSymbol: string;
  quantity: number;
  returnedQuantity: number;
  unitCost: number;
  discountAmount: number;
  vatAmount: number;
  lineTotal: number;
}

export interface PurchasePayment {
  id: string;
  paymentMethodId: string;
  paymentMethodName: string;
  amount: number;
  paidOn: string;
  referenceNumber: string | null;
  notes: string | null;
}

export interface PurchaseReturn {
  id: string;
  returnNumber: string;
  returnDate: string;
  totalAmount: number;
  reason: string;
}

export interface PurchaseDetail extends PurchaseListItem {
  subtotal: number;
  discountAmount: number;
  vatAmount: number;
  notes: string | null;
  items: PurchaseLine[];
  payments: PurchasePayment[];
  returns: PurchaseReturn[];
}

export interface SavePurchaseItemRequest {
  productId: string;
  quantity: number;
  unitCost: number;
  discountAmount: number;
  vatAmount: number;
}

export interface CreatePurchaseRequest {
  supplierId: string;
  supplierInvoiceNumber: string | null;
  purchaseDate: string;
  notes: string | null;
  items: SavePurchaseItemRequest[];
  payments: Omit<RecordPurchasePaymentRequest, 'paidOn'>[];
}

export interface RecordPurchasePaymentRequest {
  paymentMethodId: string;
  amount: number;
  paidOn: string;
  referenceNumber: string | null;
  notes: string | null;
}

export interface PurchaseReturnItemRequest {
  purchaseDetailId: string;
  quantity: number;
}

export interface CreatePurchaseReturnRequest {
  returnDate: string;
  reason: string;
  notes: string | null;
  items: PurchaseReturnItemRequest[];
}

export type SupplierLedgerEntryType = 'Purchase' | 'Payment' | 'PurchaseReturn' | 'Adjustment';

export interface SupplierLedgerEntry {
  id: string;
  entryDate: string;
  entryType: SupplierLedgerEntryType;
  debit: number;
  credit: number;
  balance: number;
  referenceType: string;
  referenceId: string;
  referenceNumber: string;
  notes: string | null;
}

export interface SupplierLedger {
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  currentBalance: number;
  entries: PagedResult<SupplierLedgerEntry>;
}
