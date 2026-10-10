import { Routes } from '@angular/router';
import { permissionGuard, permissionRedirectGuard } from '../../core/auth/auth.guard';
import { permissions } from '../../core/auth/permissions';
import { CustomerLedgerPage } from './customer-ledger-page';
import { PosCheckoutPage } from './pos-checkout-page';
import { SaleDetailPage } from './sale-detail-page';
import { SalePaymentPage } from './sale-payment-page';
import { SalesHistoryPage } from './sales-history-page';

export const SALES_POS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    component: PosCheckoutPage,
    canMatch: [permissionRedirectGuard(permissions.sales.create, ['/sales-pos/history'])]
  },
  { path: 'history', component: SalesHistoryPage },
  { path: 'customer-ledger', component: CustomerLedgerPage },
  {
    path: ':id/payment',
    component: SalePaymentPage,
    canMatch: [permissionGuard(permissions.customerAccounts.collectDue)]
  },
  { path: ':id', component: SaleDetailPage }
];
