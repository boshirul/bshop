import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/auth.guard';
import { permissions } from '../../core/auth/permissions';
import { PurchaseDetailPage } from './purchase-detail-page';
import { PurchaseEditorPage } from './purchase-editor-page';
import { PurchaseListPage } from './purchase-list-page';
import { PurchasePaymentPage } from './purchase-payment-page';
import { PurchaseReturnPage } from './purchase-return-page';
import { SupplierLedgerPage } from './supplier-ledger-page';

export const PURCHASE_ROUTES: Routes = [
  { path: '', component: PurchaseListPage },
  { path: 'new', component: PurchaseEditorPage, canMatch: [permissionGuard(permissions.purchases.manage)] },
  { path: 'supplier-ledger', component: SupplierLedgerPage },
  { path: ':id/payment', component: PurchasePaymentPage, canMatch: [permissionGuard(permissions.purchases.paySupplier)] },
  { path: ':id/return', component: PurchaseReturnPage, canMatch: [permissionGuard(permissions.purchases.manage)] },
  { path: ':id', component: PurchaseDetailPage }
];
