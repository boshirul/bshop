namespace RetailShop.Application.Security;

public static class Permissions
{
    public static class Dashboard
    {
        public const string View = "dashboard.view";
    }

    public static class Products
    {
        public const string View = "products.view";
        public const string Manage = "products.manage";
        public const string Delete = "products.delete";
    }

    public static class Purchases
    {
        public const string View = "purchases.view";
        public const string Manage = "purchases.manage";
        public const string PaySupplier = "purchases.pay-supplier";
    }

    public static class Sales
    {
        public const string View = "sales.view";
        public const string Create = "sales.create";
        public const string ChangePrice = "sales.change-price";
        public const string Discount = "sales.discount";
        public const string SellOnDue = "sales.sell-on-due";
        public const string Cancel = "sales.cancel";
    }

    public static class Quotations
    {
        public const string View = "quotations.view";
        public const string Manage = "quotations.manage";
        public const string Convert = "quotations.convert";
    }

    public static class Customers
    {
        public const string View = "customers.view";
        public const string Manage = "customers.manage";
        public const string CollectDue = "customers.collect-due";
        public const string ViewAccounts = "customers.accounts.view";
        public const string AdjustLedger = "customers.adjust-ledger";
        public const string PrintStatement = "customers.print-statement";
    }

    public static class Suppliers
    {
        public const string View = "suppliers.view";
        public const string Manage = "suppliers.manage";
    }

    public static class Settings
    {
        public const string View = "settings.view";
        public const string Manage = "settings.manage";
    }

    public static class Returns
    {
        public const string Request = "returns.request";
        public const string Approve = "returns.approve";
    }

    public static class Inventory
    {
        public const string View = "inventory.view";
        public const string OpeningStock = "inventory.opening-stock";
        public const string RequestAdjustment = "inventory.request-adjustment";
        public const string ApproveAdjustment = "inventory.approve-adjustment";
    }

    public static class Warranty
    {
        public const string View = "warranty.view";
        public const string CreateClaim = "warranty.create-claim";
        public const string ApproveClaim = "warranty.approve-claim";
    }

    public static class OnlineOrders
    {
        public const string View = "online-orders.view";
        public const string Manage = "online-orders.manage";
    }

    public static class Reports
    {
        public const string Sales = "reports.sales";
        public const string Profit = "reports.profit";
        public const string Inventory = "reports.inventory";
    }

    public static class DataExchange
    {
        public const string Manage = "data-exchange.manage";
    }

    public static class Notifications
    {
        public const string View = "notifications.view";
        public const string Manage = "notifications.manage";
    }

    public static class Administration
    {
        public const string ManageUsers = "administration.manage-users";
        public const string ManageRoles = "administration.manage-roles";
        public const string ViewAuditLog = "administration.view-audit-log";
    }

    public static IReadOnlyCollection<PermissionDefinition> All { get; } =
    [
        new(Dashboard.View, "View dashboard", "Dashboard"),
        new(Products.View, "View products", "Products"),
        new(Products.Manage, "Create and edit products", "Products"),
        new(Products.Delete, "Delete products", "Products"),
        new(Purchases.View, "View purchases", "Purchases"),
        new(Purchases.Manage, "Create and edit purchases", "Purchases"),
        new(Purchases.PaySupplier, "Record supplier payments", "Purchases"),
        new(Sales.View, "View sales", "Sales"),
        new(Sales.Create, "Create POS sales", "Sales"),
        new(Sales.ChangePrice, "Change sale prices", "Sales"),
        new(Sales.Discount, "Apply sale discounts", "Sales"),
        new(Sales.SellOnDue, "Create due sales", "Sales"),
        new(Sales.Cancel, "Cancel sales", "Sales"),
        new(Quotations.View, "View quotations", "Quotations"),
        new(Quotations.Manage, "Create and manage quotations", "Quotations"),
        new(Quotations.Convert, "Convert quotations to sales", "Quotations"),
        new(Customers.View, "View customers", "Customers"),
        new(Customers.Manage, "Create and edit customers", "Customers"),
        new(Customers.CollectDue, "Collect customer dues", "Customers"),
        new(Customers.ViewAccounts, "View customer accounts", "Customers"),
        new(Customers.AdjustLedger, "Post customer ledger corrections", "Customers"),
        new(Customers.PrintStatement, "Print customer statements", "Customers"),
        new(Suppliers.View, "View suppliers", "Suppliers"),
        new(Suppliers.Manage, "Create and edit suppliers", "Suppliers"),
        new(Settings.View, "View shop settings", "Settings"),
        new(Settings.Manage, "Manage shop settings", "Settings"),
        new(Returns.Request, "Request sales returns", "Returns"),
        new(Returns.Approve, "Approve sales returns", "Returns"),
        new(Inventory.View, "View inventory", "Inventory"),
        new(Inventory.OpeningStock, "Record opening stock", "Inventory"),
        new(Inventory.RequestAdjustment, "Request stock adjustments", "Inventory"),
        new(Inventory.ApproveAdjustment, "Approve stock adjustments", "Inventory"),
        new(Warranty.View, "View warranty claims", "Warranty"),
        new(Warranty.CreateClaim, "Create warranty claims", "Warranty"),
        new(Warranty.ApproveClaim, "Approve warranty claims", "Warranty"),
        new(OnlineOrders.View, "View online orders", "Online Orders"),
        new(OnlineOrders.Manage, "Manage online orders", "Online Orders"),
        new(Reports.Sales, "View sales reports", "Reports"),
        new(Reports.Profit, "View profit reports", "Reports"),
        new(Reports.Inventory, "View inventory reports", "Reports"),
        new(DataExchange.Manage, "Import and export data", "Data Exchange"),
        new(Notifications.View, "View notification center", "Notifications"),
        new(Notifications.Manage, "Generate and update notifications", "Notifications"),
        new(Administration.ManageUsers, "Manage users", "Administration"),
        new(Administration.ManageRoles, "Manage roles and permissions", "Administration"),
        new(Administration.ViewAuditLog, "View audit logs", "Administration")
    ];
}

public sealed record PermissionDefinition(
    string Name,
    string DisplayName,
    string Group);
