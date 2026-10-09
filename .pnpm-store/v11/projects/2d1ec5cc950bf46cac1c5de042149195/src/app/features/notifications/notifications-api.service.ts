import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  BackgroundJobRunItem,
  NotificationGenerationResult,
  NotificationKind,
  NotificationMessageItem,
  NotificationStatus,
  PagedResult
} from './notifications.models';

@Injectable({ providedIn: 'root' })
export class NotificationsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  messages(kind: NotificationKind | '' = '', status: NotificationStatus | '' = ''):
    Observable<PagedResult<NotificationMessageItem>> {
    let params = new HttpParams().set('pageSize', 50);
    if (kind) params = params.set('kind', kind);
    if (status) params = params.set('status', status);
    return this.get('notifications', params);
  }

  jobs(): Observable<PagedResult<BackgroundJobRunItem>> {
    return this.get('notifications/jobs', new HttpParams().set('pageSize', 20));
  }

  generate(path: string): Observable<NotificationGenerationResult> {
    return this.post(`notifications/generate/${path}`);
  }

  markCopied(id: string): Observable<boolean> {
    return this.post(`notifications/${id}/copied`);
  }

  dismiss(id: string): Observable<boolean> {
    return this.post(`notifications/${id}/dismiss`);
  }

  private get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.base}/${path}`, { params })
      .pipe(map(response => this.data(response)));
  }

  private post<T>(path: string): Observable<T> {
    return this.http.post<ApiResponse<T>>(`${this.base}/${path}`, {})
      .pipe(map(response => this.data(response)));
  }

  private data<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
