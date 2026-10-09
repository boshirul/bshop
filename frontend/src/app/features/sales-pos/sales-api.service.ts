import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  CreateSaleRequest,
  CustomerLedger,
  PagedResult,
  RecordSalePaymentRequest,
  SaleDetail,
  SaleListItem
} from './sales.models';

@Injectable({ providedIn: 'root' })
export class SalesApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getSales(search = '', page = 1, pageSize = 25): Observable<PagedResult<SaleListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.get<PagedResult<SaleListItem>>('sales', params);
  }

  getSale(id: string): Observable<SaleDetail> {
    return this.get<SaleDetail>(`sales/${id}`);
  }

  createSale(request: CreateSaleRequest): Observable<SaleDetail> {
    return this.post<SaleDetail>('sales', request);
  }

  recordPayment(id: string, request: RecordSalePaymentRequest): Observable<SaleDetail> {
    return this.post<SaleDetail>(`sales/${id}/payments`, request);
  }

  cancelSale(id: string, reason: string): Observable<SaleDetail> {
    return this.post<SaleDetail>(`sales/${id}/cancel`, { reason });
  }

  getCustomerLedger(customerId: string, page = 1, pageSize = 50): Observable<CustomerLedger> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.get<CustomerLedger>(`customers/${customerId}/ledger`, params);
  }

  private get<T>(endpoint: string, params?: HttpParams): Observable<T> {
    return this.http
      .get<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, { params })
      .pipe(map(response => this.requireData(response)));
  }

  private post<T>(endpoint: string, request: unknown): Observable<T> {
    return this.http
      .post<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, request)
      .pipe(map(response => this.requireData(response)));
  }

  private requireData<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
