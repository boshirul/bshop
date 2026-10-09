import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  ConvertQuotationRequest,
  CreateQuotationRequest,
  PagedResult,
  QuotationDetail,
  QuotationListItem
} from './quotation.models';
import { SaleDetail } from '../sales-pos/sales.models';

@Injectable({ providedIn: 'root' })
export class QuotationApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getQuotations(search = '', page = 1, pageSize = 25): Observable<PagedResult<QuotationListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.get<PagedResult<QuotationListItem>>('quotations', params);
  }

  getQuotation(id: string): Observable<QuotationDetail> {
    return this.get<QuotationDetail>(`quotations/${id}`);
  }

  createQuotation(request: CreateQuotationRequest): Observable<QuotationDetail> {
    return this.post<QuotationDetail>('quotations', request);
  }

  updateQuotation(id: string, request: CreateQuotationRequest): Observable<QuotationDetail> {
    return this.put<QuotationDetail>(`quotations/${id}`, request);
  }

  send(id: string): Observable<QuotationDetail> {
    return this.post<QuotationDetail>(`quotations/${id}/send`, {});
  }

  accept(id: string): Observable<QuotationDetail> {
    return this.post<QuotationDetail>(`quotations/${id}/accept`, {});
  }

  reject(id: string, reason: string): Observable<QuotationDetail> {
    return this.post<QuotationDetail>(`quotations/${id}/reject`, { reason });
  }

  convert(id: string, request: ConvertQuotationRequest): Observable<SaleDetail> {
    return this.post<SaleDetail>(`quotations/${id}/convert`, request);
  }

  private get<T>(endpoint: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, { params })
      .pipe(map(response => this.requireData(response)));
  }

  private post<T>(endpoint: string, request: unknown): Observable<T> {
    return this.http.post<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, request)
      .pipe(map(response => this.requireData(response)));
  }

  private put<T>(endpoint: string, request: unknown): Observable<T> {
    return this.http.put<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, request)
      .pipe(map(response => this.requireData(response)));
  }

  private requireData<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
