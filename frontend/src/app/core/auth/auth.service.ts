import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, shareReplay, tap } from 'rxjs';
import { API_BASE_URL } from '../http/api-base-url.token';
import { ApiResponse, AuthResponse, CurrentUser, LoginRequest } from './auth.models';

const sessionKey = 'khanshop.auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);
  private readonly session = signal<AuthResponse | null>(this.readSession());
  private refreshRequest?: Observable<AuthResponse>;

  readonly currentUser = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<ApiResponse<AuthResponse>>(
        `${this.apiBaseUrl}/auth/login`,
        request,
        { withCredentials: true }
      )
      .pipe(
        map((response) => this.requireData(response)),
        tap((response) => this.saveSession(response))
      );
  }

  refresh(): Observable<AuthResponse> {
    if (this.refreshRequest) {
      return this.refreshRequest;
    }

    this.refreshRequest = this.http
      .post<ApiResponse<AuthResponse>>(
        `${this.apiBaseUrl}/auth/refresh-token`,
        {},
        { withCredentials: true }
      )
      .pipe(
        map((response) => this.requireData(response)),
        tap((response) => this.saveSession(response)),
        finalize(() => {
          this.refreshRequest = undefined;
        }),
        shareReplay({ bufferSize: 1, refCount: false })
      );

    return this.refreshRequest;
  }

  ensureSession(): Observable<boolean> {
    const session = this.session();
    if (session && new Date(session.accessTokenExpiresOn).getTime() > Date.now() + 15_000) {
      return of(true);
    }

    return this.refresh().pipe(
      map(() => true),
      catchError(() => {
        this.clearSession();
        return of(false);
      })
    );
  }

  logout(): Observable<void> {
    return this.http
      .post<void>(`${this.apiBaseUrl}/auth/logout`, {}, { withCredentials: true })
      .pipe(finalize(() => this.clearSession()));
  }

  forgotPassword(email: string): Observable<string> {
    return this.http
      .post<ApiResponse<string>>(`${this.apiBaseUrl}/auth/forgot-password`, { email })
      .pipe(map((response) => response.data ?? response.message ?? 'Request accepted.'));
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<boolean> {
    return this.http
      .post<ApiResponse<boolean>>(`${this.apiBaseUrl}/auth/reset-password`, {
        email,
        token,
        newPassword
      })
      .pipe(map((response) => response.succeeded));
  }

  hasPermission(permission: string): boolean {
    return this.currentUser()?.permissions.includes(permission) ?? false;
  }

  private saveSession(session: AuthResponse): void {
    this.session.set(session);
    globalThis.sessionStorage?.setItem(sessionKey, JSON.stringify(session));
  }

  private clearSession(): void {
    this.session.set(null);
    globalThis.sessionStorage?.removeItem(sessionKey);
  }

  private readSession(): AuthResponse | null {
    try {
      const value = globalThis.sessionStorage?.getItem(sessionKey);
      return value ? (JSON.parse(value) as AuthResponse) : null;
    } catch {
      return null;
    }
  }

  private requireData(response: ApiResponse<AuthResponse>): AuthResponse {
    if (!response.succeeded || !response.data) {
      throw new Error(response.errors[0] ?? 'Authentication failed.');
    }

    return response.data;
  }
}
