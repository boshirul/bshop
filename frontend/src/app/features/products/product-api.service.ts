import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  NamedMasterDataItem,
  PagedResult,
  ProductDetail,
  ProductImageItem,
  ProductListItem,
  ProductModelItem,
  SaveProductRequest,
  SubCategoryItem,
  UnitItem
} from './product.models';

@Injectable({ providedIn: 'root' })
export class ProductApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getProducts(search = '', page = 1, pageSize = 25): Observable<PagedResult<ProductListItem>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (search.trim()) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<ApiResponse<PagedResult<ProductListItem>>>(
        `${this.apiBaseUrl}/products`,
        { params }
      )
      .pipe(map((response) => this.requireData(response)));
  }

  getProduct(id: string): Observable<ProductDetail> {
    return this.http
      .get<ApiResponse<ProductDetail>>(`${this.apiBaseUrl}/products/${id}`)
      .pipe(map((response) => this.requireData(response)));
  }

  createProduct(request: SaveProductRequest): Observable<ProductDetail> {
    return this.http
      .post<ApiResponse<ProductDetail>>(`${this.apiBaseUrl}/products`, request)
      .pipe(map((response) => this.requireData(response)));
  }

  updateProduct(id: string, request: SaveProductRequest): Observable<ProductDetail> {
    return this.http
      .put<ApiResponse<ProductDetail>>(`${this.apiBaseUrl}/products/${id}`, request)
      .pipe(map((response) => this.requireData(response)));
  }

  deleteProduct(id: string): Observable<boolean> {
    return this.http
      .delete<ApiResponse<boolean>>(`${this.apiBaseUrl}/products/${id}`)
      .pipe(map((response) => this.requireData(response)));
  }

  addImage(productId: string, url: string): Observable<ProductImageItem> {
    return this.http
      .post<ApiResponse<ProductImageItem>>(
        `${this.apiBaseUrl}/products/${productId}/images`,
        { url, altText: null, isPrimary: true }
      )
      .pipe(map((response) => this.requireData(response)));
  }

  getCategories(): Observable<NamedMasterDataItem[]> {
    return this.getCollection<NamedMasterDataItem>('categories');
  }

  getSubCategories(categoryId?: string): Observable<SubCategoryItem[]> {
    const params = categoryId ? new HttpParams().set('categoryId', categoryId) : undefined;
    return this.http
      .get<ApiResponse<SubCategoryItem[]>>(`${this.apiBaseUrl}/subcategories`, {
        params
      })
      .pipe(map((response) => this.requireData(response)));
  }

  getBrands(): Observable<NamedMasterDataItem[]> {
    return this.getCollection<NamedMasterDataItem>('brands');
  }

  getModels(brandId?: string): Observable<ProductModelItem[]> {
    const params = brandId ? new HttpParams().set('brandId', brandId) : undefined;
    return this.http
      .get<ApiResponse<ProductModelItem[]>>(`${this.apiBaseUrl}/product-models`, {
        params
      })
      .pipe(map((response) => this.requireData(response)));
  }

  getUnits(): Observable<UnitItem[]> {
    return this.getCollection<UnitItem>('units');
  }

  getMasterItems(kind: MasterDataKind): Observable<MasterDataItem[]> {
    switch (kind) {
      case 'categories':
        return this.getCategories();
      case 'subcategories':
        return this.getSubCategories();
      case 'brands':
        return this.getBrands();
      case 'models':
        return this.getModels();
      case 'units':
        return this.getUnits();
    }
  }

  saveMasterItem(
    kind: MasterDataKind,
    id: string | null,
    request: Record<string, unknown>
  ): Observable<MasterDataItem> {
    const endpoint = this.endpointFor(kind);
    const call = id
      ? this.http.put<ApiResponse<MasterDataItem>>(
          `${this.apiBaseUrl}/${endpoint}/${id}`,
          request
        )
      : this.http.post<ApiResponse<MasterDataItem>>(
          `${this.apiBaseUrl}/${endpoint}`,
          request
        );
    return call.pipe(map((response) => this.requireData(response)));
  }

  deleteMasterItem(kind: MasterDataKind, id: string): Observable<boolean> {
    return this.http
      .delete<ApiResponse<boolean>>(
        `${this.apiBaseUrl}/${this.endpointFor(kind)}/${id}`
      )
      .pipe(map((response) => this.requireData(response)));
  }

  private getCollection<T>(endpoint: string): Observable<T[]> {
    return this.http
      .get<ApiResponse<T[]>>(`${this.apiBaseUrl}/${endpoint}`)
      .pipe(map((response) => this.requireData(response)));
  }

  private endpointFor(kind: MasterDataKind): string {
    return kind === 'models' ? 'product-models' : kind;
  }

  private requireData<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }

    return response.data;
  }
}

export type MasterDataKind =
  | 'categories'
  | 'subcategories'
  | 'brands'
  | 'models'
  | 'units';

export type MasterDataItem =
  | NamedMasterDataItem
  | SubCategoryItem
  | ProductModelItem
  | UnitItem;
