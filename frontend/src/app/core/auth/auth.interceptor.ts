import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
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
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthEndpoint) {
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
        catchError((refreshError) => throwError(() => refreshError))
      );
    })
  );
};
