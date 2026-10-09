namespace RetailShop.Domain.Inventory;

public enum StockBucket
{
    Available = 1,
    Reserved = 2,
    Damaged = 3,
    Warranty = 4,
    SupplierClaim = 5
}

public enum StockTransactionType
{
    Opening = 1,
    Purchase = 2,
    Sale = 3,
    SalesReturn = 4,
    PurchaseReturn = 5,
    Damage = 6,
    AdjustmentIn = 7,
    AdjustmentOut = 8,
    WarrantyReplacement = 9,
    OnlineReservation = 10,
    ReservationRelease = 11,
    AdjustmentReversalIn = 12,
    AdjustmentReversalOut = 13,
    SaleCancellation = 14,
    WarrantyClaim = 15,
    SupplierClaim = 16,
    WarrantyRepair = 17,
    OnlineDelivery = 18
}

public enum StockAdjustmentDirection
{
    Increase = 1,
    Decrease = 2
}

public enum StockAdjustmentStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Reversed = 4
}
