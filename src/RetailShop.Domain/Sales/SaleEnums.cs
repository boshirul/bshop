namespace RetailShop.Domain.Sales;

public enum SaleStatus
{
    Completed,
    Cancelled
}

public enum SalePaymentStatus
{
    Unpaid,
    Partial,
    Paid
}

public enum CustomerLedgerEntryType
{
    Sale,
    Payment,
    Cancellation,
    Refund,
    DueCollection,
    ManualAdjustment
}
