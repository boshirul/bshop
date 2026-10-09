import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/auth.guard';
import { permissions } from '../../core/auth/permissions';
import { QuotationDetailPage } from './quotation-detail-page';
import { QuotationEditorPage } from './quotation-editor-page';
import { QuotationListPage } from './quotation-list-page';

export const QUOTATION_ROUTES: Routes = [
  { path: '', component: QuotationListPage },
  { path: 'new', component: QuotationEditorPage, canMatch: [permissionGuard(permissions.quotations.manage)] },
  { path: ':id/edit', component: QuotationEditorPage, canMatch: [permissionGuard(permissions.quotations.manage)] },
  { path: ':id', component: QuotationDetailPage }
];
