import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/auth.guard';
import { permissions } from '../../core/auth/permissions';
import { OpeningStockPage } from './opening-stock-page';
import { StockAdjustmentsPage } from './stock-adjustments-page';
import { StockLedgerPage } from './stock-ledger-page';
import { StockListPage } from './stock-list-page';

export const INVENTORY_ROUTES: Routes = [
  {
    path: '',
    component: StockListPage,
    data: { view: 'current' }
  },
  {
    path: 'low-stock',
    component: StockListPage,
    data: { view: 'low-stock' }
  },
  {
    path: 'damaged',
    component: StockListPage,
    data: { view: 'damaged' }
  },
  {
    path: 'opening',
    canMatch: [permissionGuard(permissions.inventory.openingStock)],
    component: OpeningStockPage
  },
  {
    path: 'ledger',
    component: StockLedgerPage
  },
  {
    path: 'adjustments',
    component: StockAdjustmentsPage
  }
];
