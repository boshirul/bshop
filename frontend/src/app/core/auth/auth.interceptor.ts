import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.accessToken();
  const isAuthEndpoint =
    request.url.includes('/auth/login') ||
    request.url.includes('/auth/refresh-token') ||
    request.url.includes('/auth/forgot-password') ||
    request.url.includes('/auth/reset-password');

  const authenticatedRequest = token
    ? request.clone({
        setHeaders: { Authorization: `Bearer ${token}` },
        withCredentials: true
      })
    : request.clone({ withCredentials: true });

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (
        !(error instanceof HttpErrorResponse) ||
        error.status !== 401 ||
        isAuthEndpoint ||
        !auth.isAuthenticated()
      ) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap((session) =>
          next(
            request.clone({
              setHeaders: { Authorization: `Bearer ${session.accessToken}` },
              withCredentials: true
            })
          )
        ),
        catchError((refreshError: unknown) => {
          if (auth.isDefinitiveRefreshFailure(refreshError) && auth.expireSession()) {
            const returnUrl = router.url.startsWith('/') && !router.url.startsWith('//')
              ? router.url
              : '/dashboard';
            void router.navigate(['/auth/login'], {
              queryParams: { returnUrl, sessionExpired: '1' }
            });
          }

          return throwError(() => refreshError);
        })
      );
    })
  );
};
