import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  PagedResult,
  PartyItem,
  PartyKind,
  SavePartyRequest
} from './contact.models';

@Injectable({ providedIn: 'root' })
export class ContactApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  get(
    kind: PartyKind,
    search = '',
    page = 1,
    pageSize = 25
  ): Observable<PagedResult<PartyItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http
      .get<ApiResponse<PagedResult<PartyItem>>>(`${this.apiBaseUrl}/${kind}`, {
        params
      })
      .pipe(map((response) => this.requireData(response)));
  }

  save(
    kind: PartyKind,
    id: string | null,
    request: SavePartyRequest
  ): Observable<PartyItem> {
    const call = id
      ? this.http.put<ApiResponse<PartyItem>>(
          `${this.apiBaseUrl}/${kind}/${id}`,
          request
        )
      : this.http.post<ApiResponse<PartyItem>>(
          `${this.apiBaseUrl}/${kind}`,
          request
        );
    return call.pipe(map((response) => this.requireData(response)));
  }

  delete(kind: PartyKind, id: string): Observable<boolean> {
    return this.http
      .delete<ApiResponse<boolean>>(`${this.apiBaseUrl}/${kind}/${id}`)
      .pipe(map((response) => this.requireData(response)));
  }

  private requireData<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
