import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  OnlineOrderDetail,
  OnlineOrderListItem,
  OnlineOrderSource,
  OnlineOrderStatus,
  PagedResult
} from '../online-store/online-store.models';

@Injectable({ providedIn: 'root' })
export class OnlineOrdersApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  list(search = '', status: OnlineOrderStatus | '' = '', source: OnlineOrderSource | '' = '', page = 1):
    Observable<PagedResult<OnlineOrderListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    if (search.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    if (source) params = params.set('source', source);
    return this.get('online-orders', params);
  }

  detail(id: string): Observable<OnlineOrderDetail> {
    return this.get(`online-orders/${id}`);
  }

  confirm(id: string, notes: string | null): Observable<OnlineOrderDetail> {
    return this.post(`online-orders/${id}/confirm`, { notes });
  }

  courier(id: string, body: unknown): Observable<OnlineOrderDetail> {
    return this.post(`online-orders/${id}/courier`, body);
  }

  cancel(id: string, reason: string): Observable<OnlineOrderDetail> {
    return this.post(`online-orders/${id}/cancel`, { reason });
  }

  deliver(id: string, notes: string | null): Observable<OnlineOrderDetail> {
    return this.post(`online-orders/${id}/deliver`, { notes });
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
