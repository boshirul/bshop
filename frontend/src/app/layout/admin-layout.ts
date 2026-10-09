import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { permissions } from '../core/auth/permissions';

interface NavigationItem {
  label: string;
  route: string;
  permission?: string;
}

@Component({
  selector: 'app-admin-layout',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatButtonModule,
    MatListModule,
    MatSidenavModule,
    MatToolbarModule
  ],
  templateUrl: './admin-layout.html',
  styleUrl: './admin-layout.scss'
})
export class AdminLayout {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly navigation: readonly NavigationItem[] = [
    { label: 'Dashboard', route: '/dashboard' },
    { label: 'Products', route: '/products' },
    {
      label: 'Inventory',
      route: '/inventory',
      permission: permissions.inventory.view
    },
    { label: 'Purchases', route: '/purchase', permission: permissions.purchases.view },
    {
      label: 'Suppliers',
      route: '/suppliers',
      permission: permissions.suppliers.view
    },
    { label: 'POS Sales', route: '/sales-pos', permission: permissions.sales.view },
    {
      label: 'Quotations',
      route: '/quotations',
      permission: permissions.quotations.view
    },
    {
      label: 'Customers',
      route: '/customers',
      permission: permissions.customers.view
    },
    {
      label: 'Customer Dues',
      route: '/customer-accounts',
      permission: permissions.customerAccounts.view
    },
    { label: 'Returns', route: '/returns' },
    { label: 'Warranty', route: '/warranty' },
    { label: 'Online Orders', route: '/online-orders' },
    { label: 'Reports', route: '/reports' },
    {
      label: 'Data Exchange',
      route: '/data-exchange',
      permission: permissions.dataExchange.manage
    },
    {
      label: 'Notifications',
      route: '/notifications',
      permission: permissions.notifications.view
    },
    {
      label: 'Settings',
      route: '/settings',
      permission: permissions.settings.view
    },
    {
      label: 'Users & Roles',
      route: '/user-management',
      permission: permissions.administration.manageUsers
    },
    {
      label: 'Audit Log',
      route: '/audit-log',
      permission: permissions.administration.viewAuditLog
    }
  ];

  protected canShow(item: NavigationItem): boolean {
    return !item.permission || this.auth.hasPermission(item.permission);
  }

  protected logout(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigate(['/auth/login']),
      error: () => this.router.navigate(['/auth/login'])
    });
  }
}
