namespace RetailShop.Domain.Finance;

public enum AccountType { Asset, Liability, Equity, Income, Expense }
public enum NormalBalance { Debit, Credit }
public enum AccountKind { Group, Posting }
public enum FiscalPeriodStatus { Open, SoftClosed, Closed }
public enum JournalStatus { Posted, Reversed }
public enum AccountMappingType { System, PaymentMethod, ExpenseCategory }
public enum FinancialAccountType { Cash, MobileWallet, Bank, CardClearing }

public static class FinancePostingKinds
{
    public const string Manual = "Manual";
    public const string OpeningBalance = "OpeningBalance";
    public const string Reversal = "Reversal";
    public const string Sale = "Sale";
    public const string SalePayment = "SalePayment";
    public const string CustomerReceipt = "CustomerReceipt";
    public const string Purchase = "Purchase";
    public const string PurchasePayment = "PurchasePayment";
    public const string Expense = "Expense";
    public const string SalesReturn = "SalesReturn";
    public const string PurchaseReturn = "PurchaseReturn";
    public const string InventoryAdjustment = "InventoryAdjustment";
}

public static class SystemAccountKeys
{
    public const string CashInHand = "cash-in-hand";
    public const string Bkash = "bkash";
    public const string Bank = "bank";
    public const string CardClearing = "card-clearing";
    public const string CustomerReceivables = "customer-receivables";
    public const string InventoryAsset = "inventory-asset";
    public const string SupplierPayables = "supplier-payables";
    public const string CustomerAdvances = "customer-advances";
    public const string SalesRevenue = "sales-revenue";
    public const string SalesReturns = "sales-returns";
    public const string CostOfGoodsSold = "cost-of-goods-sold";
    public const string OperatingExpenses = "operating-expenses";
    public const string TaxPayable = "tax-payable";
    public const string OwnerCapital = "owner-capital";
    public const string RetainedEarnings = "retained-earnings";
    public const string OpeningBalanceEquity = "opening-balance-equity";
    public const string InventoryAdjustmentGain = "inventory-adjustment-gain";
    public const string InventoryWriteOff = "inventory-write-off";
}
