import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/auth.guard';
import { permissions } from '../../core/auth/permissions';
import { AgingPage } from './aging-page';
import { DuesDashboardPage } from './dues-dashboard-page';
import { ReceiptEditorPage } from './receipt-editor-page';
import { ReceiptDetailPage, ReceiptHistoryPage } from './receipt-pages';
import { StatementPage } from './statement-page';

export const CUSTOMER_ACCOUNT_ROUTES: Routes = [
  { path: '', component: DuesDashboardPage },
  { path: 'aging', component: AgingPage },
  { path: 'statements', component: StatementPage },
  { path: 'receipts', component: ReceiptHistoryPage },
  { path: 'receipts/new', component: ReceiptEditorPage, canMatch: [permissionGuard(permissions.customerAccounts.collectDue)] },
  { path: 'receipts/:id', component: ReceiptDetailPage }
];
