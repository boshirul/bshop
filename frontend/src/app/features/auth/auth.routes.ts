import { Routes } from '@angular/router';
import { ForgotPasswordPage } from './forgot-password-page';
import { LoginPage } from './login-page';
import { ResetPasswordPage } from './reset-password-page';

export const AUTH_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: 'login', component: LoginPage, title: 'Sign in | KhanShop' },
  {
    path: 'forgot-password',
    component: ForgotPasswordPage,
    title: 'Forgot password | KhanShop'
  },
  {
    path: 'reset-password',
    component: ResetPasswordPage,
    title: 'Reset password | KhanShop'
  }
];
