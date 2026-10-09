import { ApiResponse } from '../../core/auth/auth.models';
import { PagedResult } from '../sales-pos/sales.models';

export type { ApiResponse, PagedResult };

export type QuotationStatus = 'Draft' | 'Sent' | 'Accepted' | 'Rejected' | 'Expired' | 'Converted';

export interface QuotationListItem {
  id: string;
  quotationNumber: string;
  customerId: string;
  customerName: string;
  quotationDate: string;
  validUntil: string;
  grandTotal: number;
  status: QuotationStatus;
  convertedSaleId: string | null;
}

export interface QuotationLine {
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

export interface QuotationDetail extends QuotationListItem {
  customerPhone: string | null;
  subtotal: number;
  discountAmount: number;
  vatAmount: number;
  notes: string | null;
  terms: string | null;
  rejectionReason: string | null;
  sentOn: string | null;
  acceptedOn: string | null;
  rejectedOn: string | null;
  convertedOn: string | null;
  items: QuotationLine[];
}

export interface CreateQuotationRequest {
  customerId: string;
  quotationDate: string;
  validUntil: string;
  notes: string | null;
  terms: string | null;
  items: Array<{
    productId: string;
    quantity: number;
    unitPrice: number;
    discountAmount: number;
    vatAmount: number;
  }>;
}

export interface ConvertQuotationRequest {
  saleDate: string;
  notes: string | null;
  payments: Array<{
    paymentMethodId: string;
    amount: number;
    referenceNumber: string | null;
    notes: string | null;
  }>;
}
