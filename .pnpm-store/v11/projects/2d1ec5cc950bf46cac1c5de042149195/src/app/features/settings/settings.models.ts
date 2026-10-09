import { ApiResponse } from '../../core/auth/auth.models';

export type { ApiResponse };

export interface ShopSettings {
  shopName: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  logoUrl: string | null;
  taxRegistrationNumber: string | null;
  receiptFooter: string;
}

export interface InvoiceSettings {
  invoicePrefix: string;
  nextInvoiceNumber: number;
  termsAndConditions: string | null;
  returnPolicy: string | null;
  showTaxDetails: boolean;
  showQrCode: boolean;
}

export interface TaxSettings {
  taxName: string;
  defaultRate: number;
  isEnabled: boolean;
}

export interface SystemSettings {
  currencyCode: string;
  timeZone: string;
  dateFormat: string;
  defaultPageSize: number;
  lowStockAlertsEnabled: boolean;
}

export interface PaymentMethodItem {
  id: string;
  name: string;
  code: string;
  type: string;
  isActive: boolean;
}

export interface SavePaymentMethodRequest {
  name: string;
  code: string;
  type: string;
  isActive: boolean;
}
