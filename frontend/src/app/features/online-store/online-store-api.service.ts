import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  OnlineOrderDetail,
  OnlineProductListItem,
  PagedResult
} from './online-store.models';

@Injectable({ providedIn: 'root' })
export class OnlineStoreApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  products(search = '', page = 1, pageSize = 50):
    Observable<PagedResult<OnlineProductListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.get('online-store/products', params);
  }

  checkout(body: unknown): Observable<OnlineOrderDetail> {
    return this.post('online-store/checkout', body);
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
}
