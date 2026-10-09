namespace RetailShop.Domain.Warranty;

public enum ProductSerialStatus
{
    Available = 1,
    Sold = 2,
    InWarranty = 3,
    SupplierClaim = 4,
    Repaired = 5,
    Replaced = 6,
    Retired = 7
}

public enum WarrantyClaimStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    SupplierClaim = 4,
    Repaired = 5,
    Replaced = 6,
    Refunded = 7,
    Resolved = 8
}

public enum WarrantyResolutionAction
{
    SupplierClaim = 1,
    Repair = 2,
    Replacement = 3,
    Refund = 4,
    Resolve = 5
}

public enum WarrantyClaimHistoryAction
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    SupplierClaim = 4,
    Repaired = 5,
    Replaced = 6,
    Refunded = 7,
    Resolved = 8
}
