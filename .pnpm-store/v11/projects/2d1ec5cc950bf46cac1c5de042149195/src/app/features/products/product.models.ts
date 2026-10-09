import { ApiResponse } from '../../core/auth/auth.models';

export type { ApiResponse };

export interface NamedMasterDataItem {
  id: string;
  name: string;
  isActive: boolean;
}

export interface SubCategoryItem extends NamedMasterDataItem {
  categoryId: string;
  categoryName: string;
}

export interface ProductModelItem extends NamedMasterDataItem {
  brandId: string;
  brandName: string;
}

export interface UnitItem extends NamedMasterDataItem {
  symbol: string;
}

export interface ProductListItem {
  id: string;
  productCode: string;
  barcode: string;
  name: string;
  categoryName: string;
  brandName: string | null;
  unitSymbol: string;
  purchasePrice: number;
  salePrice: number;
  averageCost: number;
  isWarrantyAvailable: boolean;
  isSerialRequired: boolean;
  allowOnlineSale: boolean;
  isActive: boolean;
  imageUrl: string | null;
}

export interface ProductImageItem {
  id: string;
  url: string;
  altText: string | null;
  isPrimary: boolean;
}

export interface ProductDetail extends ProductListItem {
  categoryId: string;
  subCategoryId: string | null;
  subCategoryName: string | null;
  brandId: string | null;
  productModelId: string | null;
  productModelName: string | null;
  unitId: string;
  unitName: string;
  variantName: string | null;
  description: string | null;
  minimumStockLevel: number;
  warrantyMonths: number | null;
  isVatApplicable: boolean;
  images: ProductImageItem[];
}

export interface SaveProductRequest {
  name: string;
  categoryId: string;
  subCategoryId: string | null;
  brandId: string | null;
  productModelId: string | null;
  unitId: string;
  variantName: string | null;
  description: string | null;
  purchasePrice: number;
  salePrice: number;
  minimumStockLevel: number;
  isWarrantyAvailable: boolean;
  warrantyMonths: number | null;
  isSerialRequired: boolean;
  isVatApplicable: boolean;
  allowOnlineSale: boolean;
  isActive: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
