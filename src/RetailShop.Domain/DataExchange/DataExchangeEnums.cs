namespace RetailShop.Domain.DataExchange;

public enum DataExchangeKind
{
    Products = 1,
    Customers = 2,
    Suppliers = 3,
    OpeningStock = 4
}

public enum ImportBatchStatus
{
    Completed = 1,
    CompletedWithErrors = 2,
    Rejected = 3
}
