import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  CreatePurchaseRequest,
  CreatePurchaseReturnRequest,
  PagedResult,
  PurchaseDetail,
  PurchaseListItem,
  RecordPurchasePaymentRequest,
  SupplierLedger
} from './purchase.models';

@Injectable({ providedIn: 'root' })
export class PurchaseApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getPurchases(search = '', page = 1, pageSize = 25): Observable<PagedResult<PurchaseListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.get<PagedResult<PurchaseListItem>>('purchases', params);
  }

  getPurchase(id: string): Observable<PurchaseDetail> {
    return this.get<PurchaseDetail>(`purchases/${id}`);
  }

  createPurchase(request: CreatePurchaseRequest): Observable<PurchaseDetail> {
    return this.post<PurchaseDetail>('purchases', request);
  }

  recordPayment(id: string, request: RecordPurchasePaymentRequest): Observable<PurchaseDetail> {
    return this.post<PurchaseDetail>(`purchases/${id}/payments`, request);
  }

  createReturn(id: string, request: CreatePurchaseReturnRequest): Observable<PurchaseDetail> {
    return this.post<PurchaseDetail>(`purchases/${id}/returns`, request);
  }

  getSupplierLedger(supplierId: string): Observable<SupplierLedger> {
    return this.get<SupplierLedger>(`suppliers/${supplierId}/ledger`);
  }

  private get<T>(endpoint: string, params?: HttpParams): Observable<T> {
    return this.http
      .get<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, { params })
      .pipe(map((response) => this.requireData(response)));
  }

  private post<T>(endpoint: string, request: unknown): Observable<T> {
    return this.http
      .post<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, request)
      .pipe(map((response) => this.requireData(response)));
  }

  private requireData<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
