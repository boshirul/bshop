import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  OnlineOrderDetail,
  OnlineProductDetailItem,
  OnlineProductListItem,
  OnlineStoreCategoryItem,
  OnlineStorePoliciesItem,
  PagedResult
} from './online-store.models';

@Injectable({ providedIn: 'root' })
export class OnlineStoreApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  products(search = '', categoryId: string | null = null, page = 1, pageSize = 24):
    Observable<PagedResult<OnlineProductListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (categoryId) params = params.set('categoryId', categoryId);
    return this.get<PagedResult<OnlineProductListItem>>('online-store/products', params).pipe(
      map((result) => ({
        ...result,
        items: result.items.map((item) => ({
          ...item,
          imageUrl: this.resolveApiUrl(item.imageUrl)
        }))
      }))
    );
  }

  product(id: string): Observable<OnlineProductDetailItem> {
    return this.get<OnlineProductDetailItem>(`online-store/products/${id}`).pipe(
      map((item) => this.resolveProductMedia(item))
    );
  }

  categories(): Observable<OnlineStoreCategoryItem[]> { return this.get('online-store/categories'); }
  policies(): Observable<OnlineStorePoliciesItem> { return this.get('online-store/policies'); }

  checkout(body: unknown): Observable<OnlineOrderDetail> {
    return this.post('online-store/checkout', body);
  }

  media(productId: string): Observable<OnlineProductDetailItem['media']> {
    return this.get<OnlineProductDetailItem['media']>(`online-store/products/${productId}/media`)
      .pipe(map((items) => items.map((item) => ({
        ...item,
        url: this.resolveApiUrl(item.url) ?? item.url,
        thumbnailUrl: this.resolveApiUrl(item.thumbnailUrl)
      }))));
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.base}/${path}`, { params })
      .pipe(map(response => this.data(response)));
  }

  private post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<ApiResponse<T>>(`${this.base}/${path}`, body)
      .pipe(map(response => this.data(response)));
  }

  private data<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }

  private resolveProductMedia(item: OnlineProductDetailItem): OnlineProductDetailItem {
    return {
      ...item,
      imageUrl: this.resolveApiUrl(item.imageUrl),
      media: item.media.map((media) => ({
        ...media,
        url: this.resolveApiUrl(media.url) ?? media.url,
        thumbnailUrl: this.resolveApiUrl(media.thumbnailUrl)
      }))
    };
  }

  private resolveApiUrl(url: string | null): string | null {
    if (!url?.startsWith('/api/')) return url;
    const applicationBase = this.base.replace(/\/api$/, '');
    return `${applicationBase}${url}`;
  }
}
