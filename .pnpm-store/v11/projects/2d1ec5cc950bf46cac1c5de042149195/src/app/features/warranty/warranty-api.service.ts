import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  PagedResult,
  ProductSerialItem,
  WarrantyClaimDetailItem,
  WarrantyClaimListItem,
  WarrantyClaimStatus,
  WarrantyLookupItem
} from './warranty.models';

@Injectable({ providedIn: 'root' })
export class WarrantyApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  availableSerials(productId: string): Observable<ProductSerialItem[]> {
    return this.get(`warranty/serials/available/${productId}`);
  }

  registerSerials(body: unknown): Observable<ProductSerialItem[]> {
    return this.post('warranty/serials', body);
  }

  lookupSerial(serialNumber: string): Observable<WarrantyLookupItem> {
    return this.get(`warranty/lookup/serial/${encodeURIComponent(serialNumber)}`);
  }

  lookupInvoice(invoiceNumber: string): Observable<WarrantyLookupItem[]> {
    return this.get(`warranty/lookup/invoice/${encodeURIComponent(invoiceNumber)}`);
  }

  claims(search = '', status: WarrantyClaimStatus | '' = '', page = 1):
    Observable<PagedResult<WarrantyClaimListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    if (search.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    return this.get('warranty/claims', params);
  }

  claim(id: string): Observable<WarrantyClaimDetailItem> {
    return this.get(`warranty/claims/${id}`);
  }

  createClaim(body: unknown): Observable<WarrantyClaimDetailItem> {
    return this.post('warranty/claims', body);
  }

  approve(id: string, notes: string | null): Observable<WarrantyClaimDetailItem> {
    return this.post(`warranty/claims/${id}/approve`, { notes });
  }

  reject(id: string, reason: string): Observable<WarrantyClaimDetailItem> {
    return this.post(`warranty/claims/${id}/reject`, { reason });
  }

  resolve(id: string, body: unknown): Observable<WarrantyClaimDetailItem> {
    return this.post(`warranty/claims/${id}/resolve`, body);
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
