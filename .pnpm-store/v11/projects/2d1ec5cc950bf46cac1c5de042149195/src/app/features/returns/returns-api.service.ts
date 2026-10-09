import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse, ComplaintReason, PagedResult, ReturnInvoice,
  SalesReturnDetail, SalesReturnListItem, SalesReturnStatus
} from './returns.models';

@Injectable({ providedIn: 'root' })
export class ReturnsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  reasons(): Observable<ComplaintReason[]> { return this.get('returns/complaint-reasons'); }
  invoice(number: string): Observable<ReturnInvoice> {
    return this.get(`returns/invoice/${encodeURIComponent(number)}`);
  }
  list(search = '', status: SalesReturnStatus | '' = '', page = 1):
    Observable<PagedResult<SalesReturnListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    if (search.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    return this.get('returns', params);
  }
  detail(id: string): Observable<SalesReturnDetail> { return this.get(`returns/${id}`); }
  create(body: unknown): Observable<SalesReturnDetail> { return this.post('returns', body); }
  approve(id: string, body: unknown): Observable<SalesReturnDetail> {
    return this.post(`returns/${id}/approve`, body);
  }
  reject(id: string, notes: string): Observable<SalesReturnDetail> {
    return this.post(`returns/${id}/reject`, { notes });
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
