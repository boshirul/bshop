export const permissions = {
  dashboard: {
    view: 'dashboard.view'
  },
  products: {
    view: 'products.view',
    manage: 'products.manage',
    delete: 'products.delete'
  },
  customers: {
    view: 'customers.view',
    manage: 'customers.manage'
  },
  suppliers: {
    view: 'suppliers.view',
    manage: 'suppliers.manage'
  },
  settings: {
    view: 'settings.view',
    manage: 'settings.manage'
  },
  inventory: {
    view: 'inventory.view',
    openingStock: 'inventory.opening-stock',
    requestAdjustment: 'inventory.request-adjustment',
    approveAdjustment: 'inventory.approve-adjustment'
  },
  purchases: {
    view: 'purchases.view',
    manage: 'purchases.manage',
    paySupplier: 'purchases.pay-supplier'
  },
  sales: {
    view: 'sales.view',
    create: 'sales.create',
    changePrice: 'sales.change-price',
    discount: 'sales.discount',
    sellOnDue: 'sales.sell-on-due',
    cancel: 'sales.cancel'
  },
  quotations: {
    view: 'quotations.view',
    manage: 'quotations.manage',
    convert: 'quotations.convert'
  },
  customerAccounts: {
    view: 'customers.accounts.view',
    collectDue: 'customers.collect-due',
    adjustLedger: 'customers.adjust-ledger',
    printStatement: 'customers.print-statement'
  },
  returns: {
    request: 'returns.request',
    approve: 'returns.approve'
  },
  warranty: {
    view: 'warranty.view',
    createClaim: 'warranty.create-claim',
    approveClaim: 'warranty.approve-claim'
  },
  onlineOrders: {
    view: 'online-orders.view',
    manage: 'online-orders.manage'
  },
  dataExchange: {
    manage: 'data-exchange.manage'
  },
  notifications: {
    view: 'notifications.view',
    manage: 'notifications.manage'
  },
  administration: {
    manageUsers: 'administration.manage-users',
    manageRoles: 'administration.manage-roles',
    viewAuditLog: 'administration.view-audit-log'
  }
} as const;
