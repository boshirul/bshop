import { ApiResponse } from '../../core/auth/auth.models';
import { PagedResult } from '../products/product.models';

export type { ApiResponse, PagedResult };

export type StockBucket = 'Available' | 'Reserved' | 'Damaged' | 'Warranty' | 'SupplierClaim';
export type StockAdjustmentDirection = 'Increase' | 'Decrease';
export type StockAdjustmentStatus = 'Pending' | 'Approved' | 'Rejected' | 'Reversed';
export type StockTransactionType =
  | 'Opening'
  | 'Purchase'
  | 'Sale'
  | 'SalesReturn'
  | 'PurchaseReturn'
  | 'Damage'
  | 'AdjustmentIn'
  | 'AdjustmentOut'
  | 'WarrantyReplacement'
  | 'OnlineReservation'
  | 'ReservationRelease'
  | 'AdjustmentReversalIn'
  | 'AdjustmentReversalOut';

export interface CurrentStockItem {
  productId: string;
  productCode: string;
  barcode: string;
  productName: string;
  categoryName: string;
  unitSymbol: string;
  availableQuantity: number;
  reservedQuantity: number;
  damagedQuantity: number;
  warrantyQuantity: number;
  supplierClaimQuantity: number;
  totalQuantity: number;
  minimumStockLevel: number;
  averageCost: number;
  stockValue: number;
  isLowStock: boolean;
}

export interface StockLedgerItem {
  id: string;
  transactionDate: string;
  productId: string;
  productCode: string;
  productName: string;
  transactionType: StockTransactionType;
  bucket: StockBucket;
  quantityIn: number;
  quantityOut: number;
  balanceQuantity: number;
  costPrice: number;
  averageCostAfterTransaction: number;
  referenceType: string;
  referenceId: string;
  remarks: string | null;
}

export interface OpeningStockItemRequest {
  productId: string;
  quantity: number;
  unitCost: number;
}

export interface OpeningStockResult {
  batchId: string;
  recordedOn: string;
  productCount: number;
}

export interface StockAdjustmentLineItem {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  unitSymbol: string;
  bucket: StockBucket;
  direction: StockAdjustmentDirection;
  quantity: number;
  unitCost: number;
  appliedCost: number;
}

export interface StockAdjustmentDetail {
  id: string;
  adjustmentNumber: string;
  requestedOn: string;
  reason: string;
  notes: string | null;
  status: StockAdjustmentStatus;
  requestedBy: string;
  reviewedBy: string | null;
  reviewedOn: string | null;
  reviewNotes: string | null;
  reversedBy: string | null;
  reversedOn: string | null;
  reversalReason: string | null;
  items: StockAdjustmentLineItem[];
}
