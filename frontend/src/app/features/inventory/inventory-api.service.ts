import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  CurrentStockItem,
  OpeningStockItemRequest,
  OpeningStockResult,
  PagedResult,
  StockAdjustmentDetail,
  StockAdjustmentStatus,
  StockBucket,
  StockLedgerItem,
  StockTransactionType
} from './inventory.models';

export type StockView = 'current' | 'low-stock' | 'damaged';

@Injectable({ providedIn: 'root' })
export class InventoryApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getStock(
    view: StockView,
    search = '',
    page = 1,
    pageSize = 25
  ): Observable<PagedResult<CurrentStockItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http
      .get<ApiResponse<PagedResult<CurrentStockItem>>>(
        `${this.apiBaseUrl}/stocks/${view}`,
        { params }
      )
      .pipe(map((response) => this.requireData(response)));
  }

  recordOpeningStock(
    items: OpeningStockItemRequest[],
    remarks: string | null
  ): Observable<OpeningStockResult> {
    return this.http
      .post<ApiResponse<OpeningStockResult>>(`${this.apiBaseUrl}/stocks/opening`, {
        items,
        remarks
      })
      .pipe(map((response) => this.requireData(response)));
  }

  getLedger(
    productId: string | null,
    transactionType: StockTransactionType | null,
    bucket: StockBucket | null,
    page = 1,
    pageSize = 25
  ): Observable<PagedResult<StockLedgerItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (productId) params = params.set('productId', productId);
    if (transactionType) params = params.set('transactionType', transactionType);
    if (bucket) params = params.set('bucket', bucket);
    return this.http
      .get<ApiResponse<PagedResult<StockLedgerItem>>>(
        `${this.apiBaseUrl}/stocks/ledger`,
        { params }
      )
      .pipe(map((response) => this.requireData(response)));
  }

  getAdjustments(
    status: StockAdjustmentStatus | null,
    page = 1
  ): Observable<PagedResult<StockAdjustmentDetail>> {
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    if (status) params = params.set('status', status);
    return this.http
      .get<ApiResponse<PagedResult<StockAdjustmentDetail>>>(
        `${this.apiBaseUrl}/stocks/adjustments`,
        { params }
      )
      .pipe(map((response) => this.requireData(response)));
  }

  createAdjustment(request: {
    reason: string;
    notes: string | null;
    items: Array<{
      productId: string;
      bucket: StockBucket;
      direction: 'Increase' | 'Decrease';
      quantity: number;
      unitCost: number;
    }>;
  }): Observable<StockAdjustmentDetail> {
    return this.http
      .post<ApiResponse<StockAdjustmentDetail>>(
        `${this.apiBaseUrl}/stocks/adjustments`,
        request
      )
      .pipe(map((response) => this.requireData(response)));
  }

  approveAdjustment(id: string, notes: string | null): Observable<StockAdjustmentDetail> {
    return this.postAction(id, 'approve', { notes });
  }

  rejectAdjustment(id: string, reason: string): Observable<StockAdjustmentDetail> {
    return this.postAction(id, 'reject', { reason });
  }

  reverseAdjustment(id: string, reason: string): Observable<StockAdjustmentDetail> {
    return this.postAction(id, 'reverse', { reason });
  }

  private postAction(
    id: string,
    action: string,
    body: object
  ): Observable<StockAdjustmentDetail> {
    return this.http
      .post<ApiResponse<StockAdjustmentDetail>>(
        `${this.apiBaseUrl}/stocks/adjustments/${id}/${action}`,
        body
      )
      .pipe(map((response) => this.requireData(response)));
  }

  private requireData<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
