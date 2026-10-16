import { ApiResponse } from '../../core/auth/auth.models';
import { PagedResult } from '../products/product.models';

export type { ApiResponse, PagedResult };

export type OnlineOrderSource = 'Website' | 'Facebook' | 'Phone' | 'WhatsApp';
export type OnlineOrderStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Delivered';

export interface OnlineProductListItem {
  id: string;
  productCode: string;
  name: string;
  brandName: string | null;
  unitSymbol: string;
  salePrice: number;
  imageUrl: string | null;
  availableQuantity: number;
}

export interface OnlineStoreCategoryItem { id: string; name: string; }

export interface OnlineProductMediaItem {
  id: string;
  mediaType: 'Image' | 'Video';
  url: string;
  thumbnailUrl: string | null;
  contentType: string;
  fileSize: number;
  isPrimary: boolean;
  sortOrder: number;
}

export interface OnlineProductDetailItem extends OnlineProductListItem {
  categoryId: string;
  categoryName: string;
  description: string | null;
  isWarrantyAvailable: boolean;
  warrantyMonths: number | null;
  media: OnlineProductMediaItem[];
}

export interface OnlineStorePoliciesItem {
  termsAndConditions: string | null;
  returnPolicy: string | null;
}

export interface OnlineOrderLine {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  unitSymbol: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface OnlineOrderHistory {
  id: string;
  action: string;
  performedBy: string | null;
  performedOn: string;
  notes: string | null;
}

export interface OnlineOrderDetail {
  id: string;
  orderNumber: string;
  source: OnlineOrderSource;
  status: OnlineOrderStatus;
  customerName: string;
  customerPhone: string;
  deliveryAddress: string;
  subtotal: number;
  deliveryCharge: number;
  grandTotal: number;
  courierName: string | null;
  trackingNumber: string | null;
  notes: string | null;
  orderedOn: string;
  confirmedOn: string | null;
  cancelledOn: string | null;
  deliveredOn: string | null;
  saleId: string | null;
  invoiceNumber: string | null;
  paidAmount: number;
  dueAmount: number;
  paymentStatus: 'Unpaid' | 'Partial' | 'Paid' | null;
  items: OnlineOrderLine[];
  history: OnlineOrderHistory[];
}

export interface OnlineOrderListItem {
  id: string;
  orderNumber: string;
  source: OnlineOrderSource;
  status: OnlineOrderStatus;
  customerName: string;
  customerPhone: string;
  orderedOn: string;
  grandTotal: number;
  courierName: string | null;
  trackingNumber: string | null;
}
