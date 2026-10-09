import { ApiResponse } from '../../core/auth/auth.models';
import { PagedResult } from '../products/product.models';

export type { ApiResponse, PagedResult };
export type SalesReturnStatus = 'Pending' | 'Refunded' | 'Replaced' | 'Adjusted' | 'Rejected';
export type SalesReturnAction = 'Refund' | 'Replacement' | 'DueAdjustment';
export type ReturnProductCondition = 'Available' | 'Damaged' | 'Warranty' | 'SupplierClaim';

export interface ComplaintReason { id: string; name: string; }
export interface ReturnInvoiceLine {
  saleDetailId: string; productId: string; productCode: string; productName: string;
  unit: string; soldQuantity: number; returnedQuantity: number;
  returnableQuantity: number; unitReturnAmount: number;
}
export interface ReturnInvoice {
  saleId: string; invoiceNumber: string; customerId: string | null; customerName: string;
  saleDate: string; grandTotal: number; paidAmount: number; dueAmount: number;
  lines: ReturnInvoiceLine[];
}
export interface SalesReturnListItem {
  id: string; returnNumber: string; invoiceNumber: string; customerName: string;
  complaintReason: string; requestedAction: SalesReturnAction;
  productCondition: ReturnProductCondition; status: SalesReturnStatus;
  totalAmount: number; requestedOn: string;
}
export interface SalesReturnDetail extends SalesReturnListItem {
  saleId: string; customerId: string | null; refundedAmount: number;
  dueAdjustedAmount: number; paymentMethodName: string | null; notes: string | null;
  reviewNotes: string | null; requestedBy: string; reviewedBy: string | null;
  reviewedOn: string | null;
  items: Array<{ id: string; saleDetailId: string; productId: string; productCode: string;
    productName: string; unit: string; quantity: number; amount: number }>;
  history: Array<{ action: string; performedBy: string; performedOn: string; notes: string | null }>;
}
