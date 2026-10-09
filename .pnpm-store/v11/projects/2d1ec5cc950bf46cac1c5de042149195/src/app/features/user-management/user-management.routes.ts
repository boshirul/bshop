import { Routes } from '@angular/router';
import { UserManagementPage } from './user-management-page';

export const USER_MANAGEMENT_ROUTES: Routes = [
  { path: '', component: UserManagementPage, title: 'Users & Roles' }
];
