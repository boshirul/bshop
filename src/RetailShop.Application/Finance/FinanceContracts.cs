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

public sealed record FinanceAccountItem(Guid Id,string Code,string Name,AccountType Type,NormalBalance NormalBalance,AccountKind Kind,Guid? ParentId,bool IsActive,bool IsSystemControlled,bool HasPostings);
public sealed record SaveFinanceAccountRequest(string Code,string Name,AccountType Type,NormalBalance NormalBalance,AccountKind Kind,Guid? ParentId,bool IsActive=true);
public sealed record FinancialAccountItem(Guid Id,string Code,string Name,FinancialAccountType Type,Guid AccountId,string AccountCode,string? MaskedReference,bool IsActive,decimal BookBalance);
public sealed record SaveFinancialAccountRequest(string Code,string Name,FinancialAccountType Type,Guid AccountId,string? MaskedReference);
public sealed record MappingItem(Guid Id,AccountMappingType Type,string Key,Guid AccountId,string AccountCode,Guid? PaymentMethodId,Guid? ExpenseCategoryId,bool IsActive);
public sealed record SaveMappingRequest(AccountMappingType Type,string Key,Guid AccountId,Guid? PaymentMethodId,Guid? ExpenseCategoryId);
public sealed record FinanceSetupItem(bool AutomaticPostingEnabled,DateOnly? CutoverDate,bool MandatoryMappingsApproved,bool OpeningBalancesApproved,bool ReconciliationReady,int MissingRequiredMappings,string Mode);
public sealed record JournalQueryItem(Guid Id,string Number,DateOnly BusinessDate,string SourceType,Guid SourceId,string PostingKind,string? Description,Guid CreatedBy,Guid? ReversesJournalId,decimal Debit,decimal Credit);
public sealed record ManualJournalRequest(DateOnly BusinessDate,string Description,string? Reference,IReadOnlyCollection<JournalLineRequest> Lines);
public sealed record TransferRequest(Guid SourceFinancialAccountId,Guid DestinationFinancialAccountId,decimal Amount,decimal Fee,Guid? FeeAccountId,DateOnly TransferDate,string? Reference,string? Description);
public sealed record ReconciliationRequest(Guid FinancialAccountId,DateOnly StatementDate,decimal ExternalOpeningBalance,decimal ExternalClosingBalance,bool Complete);
public interface IFinanceOperationsService
{
 Task<FinanceSetupItem> SetupAsync(CancellationToken ct=default); Task<IReadOnlyCollection<FinanceAccountItem>> AccountsAsync(string? search,CancellationToken ct=default); Task<OperationResult<FinanceAccountItem>> SaveAccountAsync(Guid? id,SaveFinanceAccountRequest request,CancellationToken ct=default);
 Task<IReadOnlyCollection<FinancialAccountItem>> FinancialAccountsAsync(CancellationToken ct=default); Task<OperationResult<FinancialAccountItem>> CreateFinancialAccountAsync(SaveFinancialAccountRequest request,CancellationToken ct=default);
 Task<IReadOnlyCollection<MappingItem>> MappingsAsync(CancellationToken ct=default); Task<OperationResult<MappingItem>> CreateMappingAsync(SaveMappingRequest request,CancellationToken ct=default);
 Task<PagedResult<JournalQueryItem>> JournalsAsync(DateOnly? from,DateOnly? to,string? search,string? source,string? kind,Guid? accountId,int page,int pageSize,CancellationToken ct=default);
 Task<OperationResult<Guid>> SaveManualDraftAsync(ManualJournalRequest request,Guid by,CancellationToken ct=default); Task<OperationResult<JournalItem>> PostManualAsync(Guid draftId,Guid by,CancellationToken ct=default); Task<OperationResult<JournalItem>> ReverseAsync(Guid journalId,DateOnly date,string reason,Guid by,CancellationToken ct=default);
 Task<OperationResult<Guid>> TransferAsync(TransferRequest request,Guid by,CancellationToken ct=default); Task<OperationResult<Guid>> ReconcileAsync(ReconciliationRequest request,Guid by,CancellationToken ct=default);
 Task<IReadOnlyCollection<FiscalPeriod>> PeriodsAsync(CancellationToken ct=default); Task<OperationResult<FiscalPeriod>> CreatePeriodAsync(string name,DateOnly start,DateOnly end,CancellationToken ct=default); Task<OperationResult<FiscalPeriod>> ChangePeriodAsync(Guid id,FiscalPeriodStatus status,Guid by,CancellationToken ct=default);
}
public sealed record OpeningPositionItem(decimal InventoryQuantity,decimal InventoryValue,decimal CustomerReceivables,decimal SupplierPayables,decimal JournalDebits,decimal JournalCredits,bool ZeroOpeningValid);
public sealed record OpeningLineRequest(Guid AccountId,decimal Debit,decimal Credit,string? ReconciliationType=null,Guid? ReconciliationId=null);
public sealed record OpeningBatchRequest(DateOnly BusinessDate,string Description,IReadOnlyCollection<OpeningLineRequest> Lines);
public sealed record FinanceReadinessItem(bool AccountsReady,bool SystemMappingsReady,bool PaymentMappingsReady,bool ExpenseMappingsReady,bool FinancialAccountsReady,bool FiscalPeriodReady,bool OpeningBalancesReady,bool ReconciliationReady,bool JournalsBalanced,bool CanActivate,IReadOnlyCollection<string> Blockers,OpeningPositionItem Positions);
public interface IFinanceSetupService
{
 Task<OpeningPositionItem> AssessOpeningAsync(CancellationToken ct=default); Task<FinanceReadinessItem> ReadinessAsync(DateOnly cutover,CancellationToken ct=default);
 Task<OperationResult<Guid>> SaveOpeningBatchAsync(OpeningBatchRequest request,Guid by,CancellationToken ct=default); Task<OperationResult<bool>> ApproveOpeningBatchAsync(Guid id,Guid by,CancellationToken ct=default); Task<OperationResult<JournalItem>> PostOpeningBatchAsync(Guid id,Guid by,CancellationToken ct=default);
 Task<OperationResult<bool>> ActivateAsync(DateOnly cutover,Guid by,CancellationToken ct=default);
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
