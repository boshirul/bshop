import { ApiResponse } from '../../core/auth/auth.models';
import { PagedResult } from '../sales-pos/sales.models';

export type { ApiResponse, PagedResult };

export type ProductSerialStatus =
  'Available' | 'Sold' | 'InWarranty' | 'SupplierClaim' |
  'Repaired' | 'Replaced' | 'Retired';

export type WarrantyClaimStatus =
  'Pending' | 'Approved' | 'Rejected' | 'SupplierClaim' |
  'Repaired' | 'Replaced' | 'Refunded' | 'Resolved';

export type WarrantyResolutionAction =
  'SupplierClaim' | 'Repair' | 'Replacement' | 'Refund' | 'Resolve';

export type WarrantyClaimHistoryAction =
  'Submitted' | 'Approved' | 'Rejected' | 'SupplierClaim' |
  'Repaired' | 'Replaced' | 'Refunded' | 'Resolved';

export interface ProductSerialItem {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  serialNumber: string;
  status: ProductSerialStatus;
  saleId: string | null;
  invoiceNumber: string | null;
  customerName: string | null;
  warrantyStartDate: string | null;
  warrantyExpiryDate: string | null;
  warrantyActive: boolean;
}

export interface WarrantyClaimListItem {
  id: string;
  claimNumber: string;
  serialNumber: string;
  productCode: string;
  productName: string;
  invoiceNumber: string | null;
  customerName: string | null;
  status: WarrantyClaimStatus;
  requestedAction: WarrantyResolutionAction;
  requestedOn: string;
}

export interface WarrantyLookupItem {
  serial: ProductSerialItem;
  claims: WarrantyClaimListItem[];
}

export interface WarrantyClaimHistoryItem {
  id: string;
  action: WarrantyClaimHistoryAction;
  performedBy: string;
  performedOn: string;
  notes: string | null;
}

export interface WarrantyClaimDetailItem {
  id: string;
  claimNumber: string;
  serial: ProductSerialItem;
  invoiceNumber: string | null;
  customerName: string | null;
  complaint: string;
  requestedAction: WarrantyResolutionAction;
  status: WarrantyClaimStatus;
  requestedOn: string;
  reviewedOn: string | null;
  resolvedOn: string | null;
  notes: string | null;
  reviewNotes: string | null;
  history: WarrantyClaimHistoryItem[];
}
