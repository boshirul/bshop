import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  CreateReceiptRequest,
  CreateCustomerAdjustmentRequest,
  CustomerDueItem,
  CustomerReceipt,
  CustomerReceiptListItem,
  CustomerStatement,
  PagedResult,
  ReceivablesAging
} from './customer-accounts.models';

@Injectable({ providedIn: 'root' })
export class CustomerAccountsApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getDues(search = '', page = 1, pageSize = 25): Observable<PagedResult<CustomerDueItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.get<PagedResult<CustomerDueItem>>('customer-accounts/dues', params);
  }

  getReceipts(search = '', page = 1, pageSize = 25): Observable<PagedResult<CustomerReceiptListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.get<PagedResult<CustomerReceiptListItem>>('customer-accounts/receipts', params);
  }

  getReceipt(id: string): Observable<CustomerReceipt> {
    return this.get<CustomerReceipt>(`customer-accounts/receipts/${id}`);
  }

  createReceipt(request: CreateReceiptRequest): Observable<CustomerReceipt> {
    return this.post<CustomerReceipt>('customer-accounts/receipts', request);
  }

  getAging(): Observable<ReceivablesAging> {
    return this.get<ReceivablesAging>('customer-accounts/aging');
  }

  createAdjustment(request: CreateCustomerAdjustmentRequest): Observable<CustomerStatement> {
    return this.post<CustomerStatement>('customer-accounts/adjustments', request);
  }

  getStatement(customerId: string, fromDate: string, toDate: string): Observable<CustomerStatement> {
    const params = new HttpParams().set('from', fromDate).set('to', toDate);
    return this.get<CustomerStatement>(`customers/${customerId}/statement`, params);
  }

  private get<T>(endpoint: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, { params })
      .pipe(map(response => this.requireData(response)));
  }

  private post<T>(endpoint: string, body: unknown): Observable<T> {
    return this.http.post<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, body)
      .pipe(map(response => this.requireData(response)));
  }

  private requireData<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
