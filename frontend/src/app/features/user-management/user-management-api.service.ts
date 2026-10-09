import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/http/api-base-url.token';
import {
  ApiResponse,
  CreateUserRequest,
  PermissionSummary,
  RoleSummary,
  UpdateUserRequest,
  UserSummary
} from './user-management.models';

@Injectable({ providedIn: 'root' })
export class UserManagementApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  users(): Observable<UserSummary[]> {
    return this.get<UserSummary[]>('users');
  }

  createUser(request: CreateUserRequest): Observable<UserSummary> {
    return this.post<UserSummary>('users', request);
  }

  updateUser(id: string, request: UpdateUserRequest): Observable<UserSummary> {
    return this.put<UserSummary>(`users/${id}`, request);
  }

  deactivateUser(id: string): Observable<boolean> {
    return this.http.delete<ApiResponse<boolean>>(`${this.base}/users/${id}`)
      .pipe(map(response => this.data(response)));
  }

  roles(): Observable<RoleSummary[]> {
    return this.get<RoleSummary[]>('roles');
  }

  createRole(name: string): Observable<RoleSummary> {
    return this.post<RoleSummary>('roles', { name });
  }

  updateRole(id: string, name: string): Observable<RoleSummary> {
    return this.put<RoleSummary>(`roles/${id}`, { name });
  }

  updateRolePermissions(id: string, permissions: string[]): Observable<RoleSummary> {
    return this.put<RoleSummary>(`roles/${id}/permissions`, { permissions });
  }

  permissions(): Observable<PermissionSummary[]> {
    return this.get<PermissionSummary[]>('permissions');
  }

  private get<T>(path: string): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${this.base}/${path}`)
      .pipe(map(response => this.data(response)));
  }

  private post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<ApiResponse<T>>(`${this.base}/${path}`, body)
      .pipe(map(response => this.data(response)));
  }

  private put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<ApiResponse<T>>(`${this.base}/${path}`, body)
      .pipe(map(response => this.data(response)));
  }

  private data<T>(response: ApiResponse<T>): T {
    if (!response.succeeded || response.data === null) {
      throw new Error(response.errors[0] ?? 'The request could not be completed.');
    }
    return response.data;
  }
}
