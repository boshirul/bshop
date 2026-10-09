import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from './core/auth/auth.guard';
import { permissions } from './core/auth/permissions';
import { AdminLayout } from './layout/admin-layout';

export const routes: Routes = [
  {
    path: 'auth',
    loadChildren: () =>
      import('./features/auth/auth.routes').then((module) => module.AUTH_ROUTES)
  },
  {
    path: 'online-store',
    loadChildren: () =>
      import('./features/online-store/online-store.routes').then(
        (module) => module.ONLINE_STORE_ROUTES
      )
  },
  {
    path: '',
    component: AdminLayout,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then(
            (module) => module.DASHBOARD_ROUTES
          )
      },
      {
        path: 'products',
        loadChildren: () =>
          import('./features/products/products.routes').then(
            (module) => module.PRODUCT_ROUTES
          )
      },
      {
        path: 'inventory',
        canMatch: [permissionGuard(permissions.inventory.view)],
        loadChildren: () =>
          import('./features/inventory/inventory.routes').then(
            (module) => module.INVENTORY_ROUTES
          )
      },
      {
        path: 'purchase',
        canMatch: [permissionGuard(permissions.purchases.view)],
        loadChildren: () =>
          import('./features/purchase/purchase.routes').then(
            (module) => module.PURCHASE_ROUTES
          )
      },
      {
        path: 'suppliers',
        canMatch: [permissionGuard(permissions.suppliers.view)],
        loadChildren: () =>
          import('./features/suppliers/suppliers.routes').then(
            (module) => module.SUPPLIER_ROUTES
          )
      },
      {
        path: 'sales-pos',
        canMatch: [permissionGuard(permissions.sales.view)],
        loadChildren: () =>
          import('./features/sales-pos/sales-pos.routes').then(
            (module) => module.SALES_POS_ROUTES
          )
      },
        {
          path: 'quotations',
          canMatch: [permissionGuard(permissions.quotations.view)],
          loadChildren: () =>
          import('./features/quotations/quotations.routes').then(
            (module) => module.QUOTATION_ROUTES
          )
      },
      {
        path: 'customers',
        canMatch: [permissionGuard(permissions.customers.view)],
        loadChildren: () =>
          import('./features/customers/customers.routes').then(
            (module) => module.CUSTOMER_ROUTES
          )
      },
      {
        path: 'customer-accounts',
        canMatch: [permissionGuard(permissions.customerAccounts.view)],
        loadChildren: () =>
          import('./features/customer-accounts/customer-accounts.routes').then(
            (module) => module.CUSTOMER_ACCOUNT_ROUTES
          )
      },
      {
        path: 'returns',
        canMatch: [permissionGuard(permissions.returns.request)],
        loadChildren: () =>
          import('./features/returns/returns.routes').then(
            (module) => module.RETURN_ROUTES
          )
      },
      {
        path: 'warranty',
        canMatch: [permissionGuard(permissions.warranty.view)],
        loadChildren: () =>
          import('./features/warranty/warranty.routes').then(
            (module) => module.WARRANTY_ROUTES
          )
      },
      {
        path: 'online-orders',
        canMatch: [permissionGuard(permissions.onlineOrders.view)],
        loadChildren: () =>
          import('./features/online-orders/online-orders.routes').then(
            (module) => module.ONLINE_ORDER_ROUTES
          )
      },
      {
        path: 'reports',
        loadChildren: () =>
          import('./features/reports/reports.routes').then(
            (module) => module.REPORT_ROUTES
          )
      },
      {
        path: 'data-exchange',
        canMatch: [permissionGuard(permissions.dataExchange.manage)],
        loadChildren: () =>
          import('./features/data-exchange/data-exchange.routes').then(
            (module) => module.DATA_EXCHANGE_ROUTES
          )
      },
      {
        path: 'notifications',
        canMatch: [permissionGuard(permissions.notifications.view)],
        loadChildren: () =>
          import('./features/notifications/notifications.routes').then(
            (module) => module.NOTIFICATION_ROUTES
          )
      },
      {
        path: 'settings',
        canMatch: [permissionGuard(permissions.settings.view)],
        loadChildren: () =>
          import('./features/settings/settings.routes').then(
            (module) => module.SETTINGS_ROUTES
          )
      },
      {
        path: 'user-management',
        canMatch: [permissionGuard(permissions.administration.manageUsers)],
        loadChildren: () =>
          import('./features/user-management/user-management.routes').then(
            (module) => module.USER_MANAGEMENT_ROUTES
          )
      },
      {
        path: 'audit-log',
        canMatch: [permissionGuard(permissions.administration.viewAuditLog)],
        loadChildren: () =>
          import('./features/audit-log/audit-log.routes').then(
            (module) => module.AUDIT_LOG_ROUTES
          )
      }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
