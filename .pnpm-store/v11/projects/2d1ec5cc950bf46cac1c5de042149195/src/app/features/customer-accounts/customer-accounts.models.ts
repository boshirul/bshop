import { ApiResponse } from '../../core/auth/auth.models';
import { PagedResult } from '../sales-pos/sales.models';

export type { ApiResponse, PagedResult };

export interface CustomerDueItem {
  customerId: string;
  customerCode: string;
  customerName: string;
  phone: string | null;
  currentBalance: number;
  invoiceDue: number;
  outstandingInvoiceCount: number;
  oldestInvoiceDate: string | null;
  creditLimit: number;
}

export interface ReceiptAllocation {
  id: string;
  saleId: string;
  invoiceNumber: string;
  saleDate: string;
  amount: number;
}

export interface CustomerReceipt {
  id: string;
  receiptNumber: string;
  customerId: string;
  customerCode: string;
  customerName: string;
  customerPhone: string;
  customerAddress: string | null;
  receivedOn: string;
  paymentMethodId: string;
  paymentMethodName: string;
  amount: number;
  allocatedAmount: number;
  accountAppliedAmount: number;
  referenceNumber: string | null;
  notes: string | null;
  allocations: ReceiptAllocation[];
}

export interface CustomerReceiptListItem {
  id: string;
  receiptNumber: string;
  customerId: string;
  customerName: string;
  customerCode: string;
  receivedOn: string;
  paymentMethodName: string;
  amount: number;
  allocatedAmount: number;
  accountAppliedAmount: number;
  referenceNumber: string | null;
}

export interface CreateReceiptRequest {
  customerId: string;
  receivedOn: string;
  paymentMethodId: string;
  amount: number;
  referenceNumber: string | null;
  notes: string | null;
}

export interface AgingBucket {
  current: number;
  days31To60: number;
  days61To90: number;
  over90: number;
  total: number;
}

export interface CustomerAgingItem extends AgingBucket {
  customerId: string;
  customerCode: string;
  customerName: string;
}

export interface ReceivablesAging {
  current: number;
  days31To60: number;
  days61To90: number;
  over90: number;
  total: number;
  customers: CustomerAgingItem[];
}

export interface StatementEntry {
  id: string;
  entryDate: string;
  entryType: string;
  referenceNumber: string;
  referenceType: string;
  referenceId: string;
  notes: string | null;
  debit: number;
  credit: number;
  balance: number;
}

export interface CustomerStatement {
  customerId: string;
  customerCode: string;
  customerName: string;
  phone: string | null;
  address: string | null;
  from: string | null;
  to: string | null;
  openingBalance: number;
  closingBalance: number;
  entries: StatementEntry[];
}

export interface CreateCustomerAdjustmentRequest {
  customerId: string;
  adjustmentDate: string;
  direction: 'Debit' | 'Credit';
  amount: number;
  reason: string;
}
