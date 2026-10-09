import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  DataExchangeKind,
  ExportLogItem,
  ImportCommitResult,
  ImportPreviewResult,
  PagedResult
} from './data-exchange.models';

@Injectable({ providedIn: 'root' })
export class DataExchangeApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  template(kind: DataExchangeKind): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/data-exchange/templates/${kind}`, {
      observe: 'response',
      responseType: 'blob'
    });
  }

  export(kind: DataExchangeKind): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/data-exchange/exports/${kind}`, {
      observe: 'response',
      responseType: 'blob'
    });
  }

  preview(kind: DataExchangeKind, csvText: string, fileName: string | null): Observable<ImportPreviewResult> {
    return this.http.post<ApiResponse<ImportPreviewResult>>(
      `${this.base}/data-exchange/imports/preview`,
      { kind, csvText, fileName }
    ).pipe(map(response => this.data(response)));
  }

  commit(kind: DataExchangeKind, csvText: string, fileName: string | null): Observable<ImportCommitResult> {
    return this.http.post<ApiResponse<ImportCommitResult>>(
      `${this.base}/data-exchange/imports/commit`,
      { kind, csvText, fileName }
    ).pipe(map(response => this.data(response)));
  }

  exportLogs(kind: DataExchangeKind | null = null): Observable<PagedResult<ExportLogItem>> {
    let params = new HttpParams().set('pageSize', 10);
    if (kind) params = params.set('kind', kind);
    return this.http.get<ApiResponse<PagedResult<ExportLogItem>>>(
      `${this.base}/data-exchange/exports/logs`,
      { params }
    ).pipe(map(response => this.data(response)));
  }

  private data<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
