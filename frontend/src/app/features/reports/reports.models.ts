import { ApiResponse } from '../../core/auth/auth.models';

export type { ApiResponse };

export interface DashboardSummary {
  todaySales: number;
  monthSales: number;
  customerDue: number;
  stockValue: number;
  lowStockProducts: number;
  pendingOnlineOrders: number;
  pendingReturns: number;
  pendingWarrantyClaims: number;
}

export interface SalesReportItem {
  id: string;
  invoiceNumber: string;
  saleDate: string;
  customerName: string;
  grandTotal: number;
  paidAmount: number;
  dueAmount: number;
  profit: number;
}

export interface ProfitSummary {
  revenue: number;
  cost: number;
  profit: number;
  profitMarginPercent: number;
}

export interface InventoryReportItem {
  productId: string;
  productCode: string;
  productName: string;
  unitSymbol: string;
  availableQuantity: number;
  reservedQuantity: number;
  damagedQuantity: number;
  warrantyQuantity: number;
  supplierClaimQuantity: number;
  totalQuantity: number;
  averageCost: number;
  stockValue: number;
  minimumStockLevel: number;
}

export interface CustomerDueReportItem {
  customerId: string;
  customerCode: string;
  customerName: string;
  phone: string;
  creditLimit: number;
  currentBalance: number;
}

export interface StatusCountItem<T = string> {
  status: T;
  count: number;
}

export interface OperationalReport {
  onlineOrders: StatusCountItem[];
  returns: StatusCountItem[];
  warrantyClaims: StatusCountItem[];
}

export type ManagementReportKind = 'DailySales' | 'MonthlySales' | 'YearlySales' | 'Profit' | 'Inventory' | 'CustomerDues';
export interface ManagementReportCatalogueItem { kind: ManagementReportKind; displayName: string; filterType: 'date' | 'month' | 'year' | 'range' | 'search' | 'none'; permission: string; }
export interface ManagementReportCell { display: string; numeric?: number | null; }
export interface ManagementReportPreview { kind: ManagementReportKind; title: string; periodLabel: string; columns: { key: string; heading: string; isNumeric: boolean }[]; rows: { values: Record<string, ManagementReportCell> }[]; summary: { label: string; value: string; numeric?: number | null }[]; }
