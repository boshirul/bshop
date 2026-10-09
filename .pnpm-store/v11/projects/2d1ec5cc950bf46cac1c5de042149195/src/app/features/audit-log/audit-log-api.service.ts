import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import { ApiResponse, AuditLogItem, PagedResult } from './audit-log.models';

@Injectable({ providedIn: 'root' })
export class AuditLogApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  logs(search = '', from = '', to = '', page = 1): Observable<PagedResult<AuditLogItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', 25);
    if (search.trim()) params = params.set('search', search.trim());
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<ApiResponse<PagedResult<AuditLogItem>>>(`${this.base}/audit-log`, { params })
      .pipe(map(response => this.data(response)));
  }

  private data<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
