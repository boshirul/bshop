import { HttpClient, HttpEvent, HttpParams, HttpRequest } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { forkJoin, map, Observable, switchMap } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  NamedMasterDataItem,
  PagedResult,
  ProductDetail,
  ProductImageItem,
  ProductMediaItem,
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
      .pipe(
        map((response) => this.requireData(response)),
        map((result) => ({
          ...result,
          items: result.items.map((item) => ({
            ...item,
            imageUrl: this.resolveApiUrl(item.imageUrl)
          }))
        }))
      );
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

  getMedia(productId: string): Observable<ProductMediaItem[]> {
    return this.http.get<ApiResponse<ProductMediaItem[]>>(`${this.apiBaseUrl}/products/${productId}/media`)
      .pipe(
        map((response) => this.requireData(response)),
        map((media) => media.map((item) => ({
          ...item,
          url: this.resolveApiUrl(item.url) ?? item.url,
          thumbnailUrl: this.resolveApiUrl(item.thumbnailUrl)
        }))),
        switchMap((media) => forkJoin(media.map((item) => this.toPreview(item))))
      );
  }

  uploadMedia(productId: string, file: File): Observable<HttpEvent<ApiResponse<ProductMediaItem>>> {
    const data = new FormData();
    data.append('file', file, file.name);
    return this.http.request(new HttpRequest('POST', `${this.apiBaseUrl}/products/${productId}/media`, data, {
      reportProgress: true
    }));
  }

  replaceMedia(productId: string, mediaId: string, file: File): Observable<HttpEvent<ApiResponse<ProductMediaItem>>> {
    const data = new FormData();
    data.append('file', file, file.name);
    return this.http.request(new HttpRequest('PUT', `${this.apiBaseUrl}/products/${productId}/media/${mediaId}`, data, {
      reportProgress: true
    }));
  }

  replaceVideoPoster(productId: string, mediaId: string, file: File): Observable<HttpEvent<ApiResponse<ProductMediaItem>>> {
    const data = new FormData();
    data.append('file', file, file.name);
    return this.http.request(new HttpRequest('PUT', `${this.apiBaseUrl}/products/${productId}/media/${mediaId}/poster`, data, {
      reportProgress: true
    }));
  }

  deleteMedia(productId: string, mediaId: string): Observable<boolean> {
    return this.http.delete<ApiResponse<boolean>>(`${this.apiBaseUrl}/products/${productId}/media/${mediaId}`)
      .pipe(map((response) => this.requireData(response)));
  }

  setPrimaryMedia(productId: string, mediaId: string): Observable<boolean> {
    return this.http.post<ApiResponse<boolean>>(`${this.apiBaseUrl}/products/${productId}/media/${mediaId}/primary`, {})
      .pipe(map((response) => this.requireData(response)));
  }

  reorderMedia(productId: string, mediaIds: string[]): Observable<ProductMediaItem[]> {
    return this.http.put<ApiResponse<ProductMediaItem[]>>(`${this.apiBaseUrl}/products/${productId}/media/order`, { mediaIds })
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

  // Product media content is intentionally protected. Fetching it through HttpClient
  // retains the existing bearer-token authorization while object URLs keep native
  // image/video previews working in the product editor.
  private toPreview(item: ProductMediaItem): Observable<ProductMediaItem> {
    const content = this.http.get(item.url, { responseType: 'blob' });
    const thumbnail = item.thumbnailUrl
      ? this.http.get(item.thumbnailUrl, { responseType: 'blob' })
      : null;
    return thumbnail
      ? forkJoin({ content, thumbnail }).pipe(map(({ content: file, thumbnail: poster }) => ({
          ...item, url: URL.createObjectURL(file), thumbnailUrl: URL.createObjectURL(poster)
        })))
      : content.pipe(map((file) => ({ ...item, url: URL.createObjectURL(file) })));
  }

  // API contracts use /api/... paths. When the API is hosted as an IIS
  // application, retain its PathBase instead of accidentally requesting the
  // site root (for example, /api/... instead of /BShopApi/api/...).
  private resolveApiUrl(url: string | null): string | null {
    if (!url?.startsWith('/api/')) return url;
    const applicationBase = this.apiBaseUrl.replace(/\/api$/, '');
    return `${applicationBase}${url}`;
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
