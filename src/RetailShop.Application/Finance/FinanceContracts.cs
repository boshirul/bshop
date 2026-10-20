using RetailShop.Application.Common;
using RetailShop.Domain.Finance;

namespace RetailShop.Application.Finance;

public sealed record JournalLineRequest(Guid AccountId, decimal Debit, decimal Credit, string? Description = null);
public sealed record PostJournalRequest(DateOnly BusinessDate, string SourceType, Guid SourceId,
    string PostingKind, string? Description, IReadOnlyCollection<JournalLineRequest> Lines,
    bool AllowSoftClosedPeriod = false);
public sealed record JournalLineItem(Guid Id, Guid AccountId, string AccountCode, decimal Debit,
    decimal Credit, string? Description);
public sealed record JournalItem(Guid Id, string Number, DateOnly BusinessDate, string SourceType,
    Guid SourceId, string PostingKind, JournalStatus Status, Guid? ReversesJournalId,
    IReadOnlyCollection<JournalLineItem> Lines);

public interface IJournalPostingService
{
    Task<OperationResult<JournalItem>> PostAsync(PostJournalRequest request, Guid performedBy,
        CancellationToken cancellationToken = default);
    Task<OperationResult<JournalItem>> ReverseAsync(Guid journalId, DateOnly businessDate,
        string reason, Guid performedBy, CancellationToken cancellationToken = default);
}

public interface IFinanceInitializer
{
    Task InitialiseAsync(CancellationToken cancellationToken = default);
}

public interface IFinanceAdministrationService
{
    Task<OperationResult<Account>> CreateAccountAsync(Account account, CancellationToken cancellationToken = default);
    Task<OperationResult<FiscalPeriod>> CreateFiscalPeriodAsync(FiscalPeriod period, CancellationToken cancellationToken = default);
    Task<OperationResult<FinancialAccount>> CreateFinancialAccountAsync(FinancialAccount account, CancellationToken cancellationToken = default);
    Task<OperationResult<AccountMapping>> SaveMappingAsync(AccountMapping mapping, CancellationToken cancellationToken = default);
}

public sealed record AccountingPayment(Guid PaymentMethodId, decimal Amount);
public sealed record AccountingExpenseLine(Guid ExpenseCategoryId, decimal Amount);
public sealed record AutomaticSalePosting(Guid SaleId, DateTimeOffset OccurredOn, decimal GrandTotal,
    decimal VatAmount, decimal CostOfGoods, IReadOnlyCollection<AccountingPayment> Payments);
public sealed record AutomaticPaymentPosting(string SourceType, Guid SourceId, DateTimeOffset OccurredOn,
    Guid PaymentMethodId, decimal Amount, bool IsCustomerReceipt, bool IsSupplierPayment = false);
public sealed record AutomaticPurchasePosting(Guid PurchaseId, DateTimeOffset OccurredOn, decimal GrandTotal,
    IReadOnlyCollection<AccountingPayment> Payments);
public sealed record AutomaticExpensePosting(Guid ExpenseId, DateOnly BusinessDate, Guid? PaymentMethodId,
    IReadOnlyCollection<AccountingExpenseLine> Lines);
public sealed record AutomaticSalesReturnPosting(Guid ReturnId, DateTimeOffset OccurredOn, decimal TotalAmount,
    decimal VatAmount, decimal RefundedAmount, decimal DueAdjustedAmount, Guid? PaymentMethodId,
    decimal InventoryCost, bool IsReplacement);
public sealed record AutomaticPurchaseReturnPosting(Guid ReturnId, DateTimeOffset OccurredOn, decimal Amount);
public sealed record AutomaticInventoryAdjustmentPosting(Guid AdjustmentId, DateTimeOffset OccurredOn,
    decimal IncreaseValue, decimal DecreaseValue);

public interface IAutomaticAccountingService
{
    Task<OperationResult<bool>> PostSaleAsync(AutomaticSalePosting posting, Guid performedBy, CancellationToken ct = default);
    Task<OperationResult<bool>> PostPaymentAsync(AutomaticPaymentPosting posting, Guid performedBy, CancellationToken ct = default);
    Task<OperationResult<bool>> PostPurchaseAsync(AutomaticPurchasePosting posting, Guid performedBy, CancellationToken ct = default);
    Task<OperationResult<bool>> PostExpenseAsync(AutomaticExpensePosting posting, Guid performedBy, CancellationToken ct = default);
    Task<OperationResult<bool>> PostSalesReturnAsync(AutomaticSalesReturnPosting posting, Guid performedBy, CancellationToken ct = default);
    Task<OperationResult<bool>> PostPurchaseReturnAsync(AutomaticPurchaseReturnPosting posting, Guid performedBy, CancellationToken ct = default);
    Task<OperationResult<bool>> PostInventoryAdjustmentAsync(AutomaticInventoryAdjustmentPosting posting, Guid performedBy, CancellationToken ct = default);
    Task<OperationResult<bool>> ReverseAsync(string sourceType, Guid sourceId, DateTimeOffset occurredOn,
        string reason, Guid performedBy, CancellationToken ct = default,
        DateTimeOffset? originalOccurredOn = null);
}

public static class FinanceAmount
{
    public static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
