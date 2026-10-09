import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/auth.guard';
import { permissions } from '../../core/auth/permissions';
import { ReturnCreatePage, ReturnDetailPage, ReturnListPage } from './return-pages';

export const RETURN_ROUTES: Routes = [
  { path: '', component: ReturnListPage, canActivate: [permissionGuard(permissions.returns.request)] },
  { path: 'new', component: ReturnCreatePage, canActivate: [permissionGuard(permissions.returns.request)] },
  { path: ':id', component: ReturnDetailPage, canActivate: [permissionGuard(permissions.returns.request)] }
];
