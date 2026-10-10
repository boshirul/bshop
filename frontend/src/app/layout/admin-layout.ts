import { Component, HostListener, inject, signal } from '@angular/core';
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
  icon: string;
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
  templateUrl: './admin-layout.html'
})
export class AdminLayout {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly sidebarCollapsed = signal(false);
  protected readonly isMobile = signal(window.innerWidth <= 760);
  protected readonly mobileDrawerOpen = signal(false);

  protected readonly navigation: readonly NavigationItem[] = [
    { label: 'Dashboard', route: '/dashboard', icon: '◫' },
    { label: 'Products', route: '/products', icon: '◇' },
    {
      label: 'Inventory',
      route: '/inventory',
      icon: '▣',
      permission: permissions.inventory.view
    },
    { label: 'Purchases', route: '/purchase', icon: '▾', permission: permissions.purchases.view },
    {
      label: 'Suppliers',
      route: '/suppliers',
      icon: '◉',
      permission: permissions.suppliers.view
    },
    { label: 'POS Sales', route: '/sales-pos', icon: '⌁', permission: permissions.sales.view },
    {
      label: 'Quotations',
      route: '/quotations',
      icon: '▤',
      permission: permissions.quotations.view
    },
    {
      label: 'Customers',
      route: '/customers',
      icon: '◎',
      permission: permissions.customers.view
    },
    {
      label: 'Customer Dues',
      route: '/customer-accounts',
      icon: '▱',
      permission: permissions.customerAccounts.view
    },
    { label: 'Returns', route: '/returns', icon: '↶' },
    { label: 'Warranty', route: '/warranty', icon: '◇' },
    { label: 'Online Orders', route: '/online-orders', icon: '⌘' },
    { label: 'Reports', route: '/reports', icon: '▥' },
    {
      label: 'Data Exchange',
      route: '/data-exchange',
      icon: '↔',
      permission: permissions.dataExchange.manage
    },
    {
      label: 'Notifications',
      route: '/notifications',
      icon: '♧',
      permission: permissions.notifications.view
    },
    {
      label: 'Settings',
      route: '/settings',
      icon: '⚙',
      permission: permissions.settings.view
    },
    {
      label: 'Users & Roles',
      route: '/user-management',
      icon: '♙',
      permission: permissions.administration.manageUsers
    },
    {
      label: 'Audit Log',
      route: '/audit-log',
      icon: '◷',
      permission: permissions.administration.viewAuditLog
    }
  ];

  @HostListener('window:resize')
  protected updateViewportState(): void {
    const mobile = window.innerWidth <= 760;
    this.isMobile.set(mobile);
    if (!mobile) this.mobileDrawerOpen.set(false);
  }

  protected toggleNavigation(): void {
    if (this.isMobile()) {
      this.mobileDrawerOpen.set(!this.mobileDrawerOpen());
      return;
    }
    this.sidebarCollapsed.set(!this.sidebarCollapsed());
  }

  protected closeMobileNavigation(): void {
    if (this.isMobile()) this.mobileDrawerOpen.set(false);
  }

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
