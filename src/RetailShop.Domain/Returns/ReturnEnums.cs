namespace RetailShop.Domain.Returns;

public enum SalesReturnStatus
{
    Pending = 1,
    Refunded = 2,
    Replaced = 3,
    Adjusted = 4,
    Rejected = 5
}

public enum SalesReturnAction
{
    Refund = 1,
    Replacement = 2,
    DueAdjustment = 3
}

public enum ReturnProductCondition
{
    Available = 1,
    Damaged = 2,
    Warranty = 3,
    SupplierClaim = 4
}

public enum ReturnApprovalAction
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3
}
