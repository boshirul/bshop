import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.ensureSession().pipe(
    map((authenticated) =>
      authenticated
        ? true
        : router.createUrlTree(['/auth/login'], {
            queryParams: { returnUrl: state.url }
          })
    )
  );
};

export function permissionGuard(permission: string): CanMatchFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.hasPermission(permission)
      ? true
      : router.createUrlTree(['/dashboard']);
  };
}

export function permissionRedirectGuard(
  permission: string,
  redirectTo: string[]
): CanMatchFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.hasPermission(permission)
      ? true
      : router.createUrlTree(redirectTo);
  };
}
