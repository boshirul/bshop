import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  CustomerDueReportItem,
  DashboardSummary,
  InventoryReportItem,
  OperationalReport,
  ProfitSummary,
  SalesReportItem
} from './reports.models';

@Injectable({ providedIn: 'root' })
export class ReportsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  dashboard(): Observable<DashboardSummary> {
    return this.get('dashboard/summary');
  }

  sales(from: string, to: string): Observable<SalesReportItem[]> {
    return this.get('reports/sales', this.range(from, to));
  }

  profit(from: string, to: string): Observable<ProfitSummary> {
    return this.get('reports/profit', this.range(from, to));
  }

  inventory(search = ''): Observable<InventoryReportItem[]> {
    let params = new HttpParams();
    if (search.trim()) params = params.set('search', search.trim());
    return this.get('reports/inventory', params);
  }

  customerDues(): Observable<CustomerDueReportItem[]> {
    return this.get('reports/customer-dues');
  }

  operations(): Observable<OperationalReport> {
    return this.get('reports/operations');
  }

  private range(from: string, to: string): HttpParams {
    return new HttpParams().set('from', from).set('to', to);
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.base}/${path}`, { params })
      .pipe(map(response => this.data(response)));
  }

  private data<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
