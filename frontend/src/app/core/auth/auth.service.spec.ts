import '@angular/compiler';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { API_BASE_URL } from '../http/api-base-url.token';
import { ApiResponse, AuthResponse } from './auth.models';
import { AuthService } from './auth.service';

const session: AuthResponse = {
  accessToken: 'access-token-for-test',
  accessTokenExpiresOn: new Date(Date.now() - 60_000).toISOString(),
  user: {
    id: '00000000-0000-0000-0000-000000000001',
    email: 'cashier@example.test',
    fullName: 'Cashier',
    roles: [],
    permissions: []
  }
};

class FakeHttpClient {
  readonly requests: Array<{ url: string; response: Subject<unknown> }> = [];

  post<T>(url: string): Observable<T> {
    const response = new Subject<unknown>();
    this.requests.push({ url, response });
    return response.asObservable() as Observable<T>;
  }

  expectOne(url: string): Subject<unknown> {
    const matches = this.requests.filter((request) => request.url === url);
    expect(matches).toHaveLength(1);
    return matches[0].response;
  }
}

describe('AuthService refresh handling', () => {
  let auth: AuthService;
  let http: FakeHttpClient;
  let injector: EnvironmentInjector;

  beforeEach(() => {
    const values = new Map<string, string>();
    vi.stubGlobal('sessionStorage', {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value),
      removeItem: (key: string) => values.delete(key)
    });
    globalThis.sessionStorage.setItem('khanshop.auth', JSON.stringify(session));
    http = new FakeHttpClient();
    TestBed.configureTestingModule({
      providers: [
        { provide: HttpClient, useValue: http },
        { provide: API_BASE_URL, useValue: '/api' }
      ]
    });
    injector = TestBed.inject(EnvironmentInjector);
    auth = runInInjectionContext(injector, () => new AuthService());
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('retains the session after a temporary refresh failure', () => {
    let authenticated: boolean | undefined;
    auth.ensureSession().subscribe((value) => (authenticated = value));
    http.expectOne('/api/auth/refresh-token').error(new HttpErrorResponse({ status: 503 }));

    expect(authenticated).toBe(true);
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('clears the session after a definitive refresh failure', () => {
    let authenticated: boolean | undefined;
    auth.ensureSession().subscribe((value) => (authenticated = value));
    http.expectOne('/api/auth/refresh-token').error(new HttpErrorResponse({ status: 401 }));

    expect(authenticated).toBe(false);
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.consumeSessionExpiry()).toBe(true);
  });

  it('shares one refresh request for concurrent callers', () => {
    const values: boolean[] = [];
    auth.ensureSession().subscribe((value) => values.push(value));
    auth.ensureSession().subscribe((value) => values.push(value));

    const refresh = http.expectOne('/api/auth/refresh-token');
    refresh.next({
      succeeded: true,
      data: { ...session, accessTokenExpiresOn: new Date(Date.now() + 15 * 60_000).toISOString() },
      message: null,
      errors: []
    } satisfies ApiResponse<AuthResponse>);
    refresh.complete();

    expect(values).toEqual([true, true]);
  });

  it('classifies only an unauthorized refresh response as a definitive session failure', () => {
    expect(auth.isDefinitiveRefreshFailure(new HttpErrorResponse({ status: 401 }))).toBe(true);
    expect(auth.isDefinitiveRefreshFailure(new HttpErrorResponse({ status: 0 }))).toBe(false);
    expect(auth.isDefinitiveRefreshFailure(new HttpErrorResponse({ status: 503 }))).toBe(false);
  });
});
