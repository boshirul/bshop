using RetailShop.Domain.Products;

namespace RetailShop.Domain.Inventory;

public sealed class StockTransaction
{
    private StockTransaction()
    {
    }

    public StockTransaction(
        Guid productId,
        StockTransactionType transactionType,
        StockBucket bucket,
        decimal quantityIn,
        decimal quantityOut,
        decimal balanceQuantity,
        decimal costPrice,
        decimal averageCostAfterTransaction,
        string referenceType,
        Guid referenceId,
        Guid createdBy,
        string? remarks)
    {
        ProductId = productId;
        TransactionType = transactionType;
        Bucket = bucket;
        QuantityIn = quantityIn;
        QuantityOut = quantityOut;
        BalanceQuantity = balanceQuantity;
        CostPrice = costPrice;
        AverageCostAfterTransaction = averageCostAfterTransaction;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        CreatedBy = createdBy;
        Remarks = remarks?.Trim();
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public DateTimeOffset TransactionDate { get; private set; } = DateTimeOffset.UtcNow;

    public StockTransactionType TransactionType { get; private set; }

    public StockBucket Bucket { get; private set; }

    public decimal QuantityIn { get; private set; }

    public decimal QuantityOut { get; private set; }

    public decimal BalanceQuantity { get; private set; }

    public decimal CostPrice { get; private set; }

    public decimal AverageCostAfterTransaction { get; private set; }

    public string ReferenceType { get; private set; } = string.Empty;

    public Guid ReferenceId { get; private set; }

    public string? Remarks { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedOn { get; private set; } = DateTimeOffset.UtcNow;
}
