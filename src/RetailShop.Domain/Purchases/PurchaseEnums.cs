namespace RetailShop.Domain.Purchases;

public enum PurchaseStatus
{
    Confirmed,
    PartiallyReturned,
    Returned
}

public enum PurchasePaymentStatus
{
    Unpaid,
    Partial,
    Paid,
    Credit
}

public enum SupplierLedgerEntryType
{
    Purchase,
    Payment,
    PurchaseReturn
}
