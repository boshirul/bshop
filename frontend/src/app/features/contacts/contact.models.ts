import { ApiResponse } from '../../core/auth/auth.models';

export type { ApiResponse };

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CustomerItem {
  id: string;
  customerCode: string;
  name: string;
  phone: string;
  email: string | null;
  address: string | null;
  notes: string | null;
  creditLimit: number;
  isActive: boolean;
}

export interface SupplierItem {
  id: string;
  supplierCode: string;
  name: string;
  contactPerson: string | null;
  phone: string;
  email: string | null;
  address: string | null;
  notes: string | null;
  isActive: boolean;
}

export interface SavePartyRequest {
  name: string;
  contactPerson?: string | null;
  phone: string;
  email: string | null;
  address: string | null;
  notes: string | null;
  creditLimit?: number;
  isActive: boolean;
}

export type PartyKind = 'customers' | 'suppliers';
export type PartyItem = CustomerItem | SupplierItem;
