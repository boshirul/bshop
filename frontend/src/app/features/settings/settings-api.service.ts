import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  InvoiceSettings,
  PaymentMethodItem,
  SavePaymentMethodRequest,
  ShopSettings,
  SystemSettings,
  TaxSettings
} from './settings.models';

@Injectable({ providedIn: 'root' })
export class SettingsApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getShop(): Observable<ShopSettings> {
    return this.get<ShopSettings>('settings/shop');
  }

  saveShop(request: ShopSettings): Observable<ShopSettings> {
    return this.put<ShopSettings>('settings/shop', request);
  }

  getInvoice(): Observable<InvoiceSettings> {
    return this.get<InvoiceSettings>('settings/invoice');
  }

  saveInvoice(request: InvoiceSettings): Observable<InvoiceSettings> {
    return this.put<InvoiceSettings>('settings/invoice', request);
  }

  getTax(): Observable<TaxSettings> {
    return this.get<TaxSettings>('settings/tax');
  }

  saveTax(request: TaxSettings): Observable<TaxSettings> {
    return this.put<TaxSettings>('settings/tax', request);
  }

  getSystem(): Observable<SystemSettings> {
    return this.get<SystemSettings>('settings/system');
  }

  saveSystem(request: SystemSettings): Observable<SystemSettings> {
    return this.put<SystemSettings>('settings/system', request);
  }

  getPaymentMethods(): Observable<PaymentMethodItem[]> {
    return this.get<PaymentMethodItem[]>('payment-methods');
  }

  savePaymentMethod(
    id: string | null,
    request: SavePaymentMethodRequest
  ): Observable<PaymentMethodItem> {
    const call = id
      ? this.http.put<ApiResponse<PaymentMethodItem>>(
          `${this.apiBaseUrl}/payment-methods/${id}`,
          request
        )
      : this.http.post<ApiResponse<PaymentMethodItem>>(
          `${this.apiBaseUrl}/payment-methods`,
          request
        );
    return call.pipe(map((response) => this.requireData(response)));
  }

  deletePaymentMethod(id: string): Observable<boolean> {
    return this.http
      .delete<ApiResponse<boolean>>(`${this.apiBaseUrl}/payment-methods/${id}`)
      .pipe(map((response) => this.requireData(response)));
  }

  private get<T>(endpoint: string): Observable<T> {
    return this.http
      .get<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`)
      .pipe(map((response) => this.requireData(response)));
  }

  private put<T>(endpoint: string, request: T): Observable<T> {
    return this.http
      .put<ApiResponse<T>>(`${this.apiBaseUrl}/${endpoint}`, request)
      .pipe(map((response) => this.requireData(response)));
  }

  private requireData<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
