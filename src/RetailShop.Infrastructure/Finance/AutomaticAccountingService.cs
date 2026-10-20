using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Common;
using RetailShop.Application.Finance;
using RetailShop.Domain.Finance;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Finance;

internal sealed class AutomaticAccountingService(
    RetailShopDbContext dbContext,
    IJournalPostingService journalPosting) : IAutomaticAccountingService
{
    public async Task<OperationResult<bool>> PostSaleAsync(AutomaticSalePosting posting, Guid by, CancellationToken ct = default)
    {
        var state = await State(posting.OccurredOn, ct); if (!state.Succeeded || !state.Value!.Enabled) return StateResult(state);
        var payments = await PaymentLines(posting.Payments, debit: true, ct); if (!payments.Succeeded) return OperationResult<bool>.Failure(payments.Errors);
        var revenue = FinanceAmount.Round(posting.GrandTotal - posting.VatAmount);
        var vat = FinanceAmount.Round(posting.VatAmount);
        var paid = FinanceAmount.Round(posting.Payments.Sum(x => x.Amount));
        var due = FinanceAmount.Round(posting.GrandTotal - paid);
        if (revenue < 0 || due < 0) return OperationResult<bool>.Failure("Sale accounting values are invalid.");
        var lines = new List<JournalLineRequest>();
        lines.AddRange(payments.Value!);
        await AddDebit(lines, SystemAccountKeys.CustomerReceivables, due, ct);
        await AddCredit(lines, SystemAccountKeys.SalesRevenue, revenue, ct);
        await AddCredit(lines, SystemAccountKeys.TaxPayable, vat, ct);
        await AddDebit(lines, SystemAccountKeys.CostOfGoodsSold, posting.CostOfGoods, ct);
        await AddCredit(lines, SystemAccountKeys.InventoryAsset, posting.CostOfGoods, ct);
        return await Post(state.Value.Date, "Sale", posting.SaleId, FinancePostingKinds.Sale,
            "Automatic sale posting", lines, by, ct);
    }

    public async Task<OperationResult<bool>> PostPaymentAsync(AutomaticPaymentPosting posting, Guid by, CancellationToken ct = default)
    {
        var state = await State(posting.OccurredOn, ct); if (!state.Succeeded || !state.Value!.Enabled) return StateResult(state);
        var financial = await PaymentAccount(posting.PaymentMethodId, ct);
        if (!financial.Succeeded) return OperationResult<bool>.Failure(financial.Errors);
        var control = await SystemAccount(posting.IsSupplierPayment
            ? SystemAccountKeys.SupplierPayables : SystemAccountKeys.CustomerReceivables, ct);
        if (!control.Succeeded) return OperationResult<bool>.Failure(control.Errors);
        return await Post(state.Value.Date, posting.SourceType, posting.SourceId,
            posting.IsSupplierPayment ? FinancePostingKinds.PurchasePayment :
                posting.IsCustomerReceipt ? FinancePostingKinds.CustomerReceipt : FinancePostingKinds.SalePayment,
            posting.IsSupplierPayment ? "Automatic supplier payment posting" : "Automatic customer collection posting",
            posting.IsSupplierPayment
                ? [new(control.Value!.Id, posting.Amount, 0), new(financial.Value!.Id, 0, posting.Amount)]
                : [new(financial.Value!.Id, posting.Amount, 0), new(control.Value!.Id, 0, posting.Amount)], by, ct);
    }

    public async Task<OperationResult<bool>> PostPurchaseAsync(AutomaticPurchasePosting posting, Guid by, CancellationToken ct = default)
    {
        var state = await State(posting.OccurredOn, ct); if (!state.Succeeded || !state.Value!.Enabled) return StateResult(state);
        var payments = await PaymentLines(posting.Payments, debit: false, ct); if (!payments.Succeeded) return OperationResult<bool>.Failure(payments.Errors);
        var paid = FinanceAmount.Round(posting.Payments.Sum(x => x.Amount));
        var due = FinanceAmount.Round(posting.GrandTotal - paid);
        if (due < 0) return OperationResult<bool>.Failure("Purchase accounting values are invalid.");
        var lines = new List<JournalLineRequest>();
        await AddDebit(lines, SystemAccountKeys.InventoryAsset, posting.GrandTotal, ct);
        lines.AddRange(payments.Value!);
        await AddCredit(lines, SystemAccountKeys.SupplierPayables, due, ct);
        return await Post(state.Value.Date, "Purchase", posting.PurchaseId, FinancePostingKinds.Purchase,
            "Automatic purchase posting", lines, by, ct);
    }

    public async Task<OperationResult<bool>> PostExpenseAsync(AutomaticExpensePosting posting, Guid by, CancellationToken ct = default)
    {
        var state = await State(posting.BusinessDate, ct); if (!state.Succeeded || !state.Value!.Enabled) return StateResult(state);
        if (!posting.PaymentMethodId.HasValue)
            return OperationResult<bool>.Failure("Posted expenses require a mapped payment method while Finance is enabled.");
        var financial = await PaymentAccount(posting.PaymentMethodId.Value, ct);
        if (!financial.Succeeded) return OperationResult<bool>.Failure(financial.Errors);
        var lines = new List<JournalLineRequest>();
        foreach (var item in posting.Lines)
        {
            var account = await ExpenseAccount(item.ExpenseCategoryId, ct);
            if (!account.Succeeded) return OperationResult<bool>.Failure(account.Errors);
            lines.Add(new(account.Value!.Id, item.Amount, 0));
        }
        lines.Add(new(financial.Value!.Id, 0, posting.Lines.Sum(x => FinanceAmount.Round(x.Amount))));
        return await Post(state.Value.Date, "ExpenseVoucher", posting.ExpenseId, FinancePostingKinds.Expense,
            "Automatic expense posting", lines, by, ct);
    }

    public async Task<OperationResult<bool>> PostSalesReturnAsync(AutomaticSalesReturnPosting posting, Guid by, CancellationToken ct = default)
    {
        var state = await State(posting.OccurredOn, ct); if (!state.Succeeded || !state.Value!.Enabled) return StateResult(state);
        if (posting.IsReplacement) return OperationResult<bool>.Success(false);
        var lines = new List<JournalLineRequest>();
        await AddDebit(lines, SystemAccountKeys.SalesReturns, posting.TotalAmount - posting.VatAmount, ct);
        await AddDebit(lines, SystemAccountKeys.TaxPayable, posting.VatAmount, ct);
        await AddCredit(lines, SystemAccountKeys.CustomerReceivables, posting.DueAdjustedAmount, ct);
        if (posting.RefundedAmount > 0)
        {
            if (!posting.PaymentMethodId.HasValue) return OperationResult<bool>.Failure("Refund payment mapping is required.");
            var financial = await PaymentAccount(posting.PaymentMethodId.Value, ct);
            if (!financial.Succeeded) return OperationResult<bool>.Failure(financial.Errors);
            lines.Add(new(financial.Value!.Id, 0, posting.RefundedAmount));
        }
        await AddDebit(lines, SystemAccountKeys.InventoryAsset, posting.InventoryCost, ct);
        await AddCredit(lines, SystemAccountKeys.CostOfGoodsSold, posting.InventoryCost, ct);
        return await Post(state.Value.Date, "SalesReturn", posting.ReturnId, FinancePostingKinds.SalesReturn,
            "Automatic sales return posting", lines, by, ct);
    }

    public async Task<OperationResult<bool>> PostPurchaseReturnAsync(AutomaticPurchaseReturnPosting posting, Guid by, CancellationToken ct = default)
    {
        var state = await State(posting.OccurredOn, ct); if (!state.Succeeded || !state.Value!.Enabled) return StateResult(state);
        var ap = await SystemAccount(SystemAccountKeys.SupplierPayables, ct);
        var inventory = await SystemAccount(SystemAccountKeys.InventoryAsset, ct);
        if (!ap.Succeeded) return OperationResult<bool>.Failure(ap.Errors);
        if (!inventory.Succeeded) return OperationResult<bool>.Failure(inventory.Errors);
        return await Post(state.Value.Date, "PurchaseReturn", posting.ReturnId, FinancePostingKinds.PurchaseReturn,
            "Automatic purchase return posting",
            [new(ap.Value!.Id, posting.Amount, 0), new(inventory.Value!.Id, 0, posting.Amount)], by, ct);
    }

    public async Task<OperationResult<bool>> PostInventoryAdjustmentAsync(AutomaticInventoryAdjustmentPosting posting, Guid by, CancellationToken ct = default)
    {
        var state = await State(posting.OccurredOn, ct); if (!state.Succeeded || !state.Value!.Enabled) return StateResult(state);
        var lines = new List<JournalLineRequest>();
        await AddDebit(lines, SystemAccountKeys.InventoryAsset, posting.IncreaseValue, ct);
        await AddCredit(lines, SystemAccountKeys.InventoryAdjustmentGain, posting.IncreaseValue, ct);
        await AddDebit(lines, SystemAccountKeys.InventoryWriteOff, posting.DecreaseValue, ct);
        await AddCredit(lines, SystemAccountKeys.InventoryAsset, posting.DecreaseValue, ct);
        return await Post(state.Value.Date, "StockAdjustment", posting.AdjustmentId,
            FinancePostingKinds.InventoryAdjustment, "Automatic inventory adjustment posting", lines, by, ct);
    }

    public async Task<OperationResult<bool>> ReverseAsync(string sourceType, Guid sourceId, DateTimeOffset occurredOn,
        string reason, Guid by, CancellationToken ct = default, DateTimeOffset? originalOccurredOn = null)
    {
        var eligibility = await State(originalOccurredOn ?? occurredOn, ct);
        if (!eligibility.Succeeded || !eligibility.Value!.Enabled) return StateResult(eligibility);
        var state = await State(occurredOn, ct);
        if (!state.Succeeded || !state.Value!.Enabled)
            return OperationResult<bool>.Failure("The reversal date is before the Finance cutover date.");
        var original = await dbContext.JournalEntries.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SourceType == sourceType && x.SourceId == sourceId && x.PostingKind != FinancePostingKinds.Reversal, ct);
        if (original is null)
            return OperationResult<bool>.Failure("The original Finance journal was not found for reversal.");
        var result = await journalPosting.ReverseAsync(original.Id, state.Value.Date, reason, by, ct);
        return result.Succeeded ? OperationResult<bool>.Success(true) : OperationResult<bool>.Failure(result.Errors);
    }

    private async Task<OperationResult<bool>> Post(DateOnly date, string sourceType, Guid sourceId, string kind,
        string description, IEnumerable<JournalLineRequest> sourceLines, Guid by, CancellationToken ct)
    {
        var lines = sourceLines.Where(x => x.Debit != 0 || x.Credit != 0)
            .GroupBy(x => x.AccountId)
            .Select(x => new JournalLineRequest(x.Key, FinanceAmount.Round(x.Sum(y => y.Debit)),
                FinanceAmount.Round(x.Sum(y => y.Credit))))
            .SelectMany(x => x.Debit > 0 && x.Credit > 0
                ? x.Debit > x.Credit
                    ? [new JournalLineRequest(x.AccountId, x.Debit - x.Credit, 0)]
                    : [new JournalLineRequest(x.AccountId, 0, x.Credit - x.Debit)]
                : new[] { x })
            .Where(x => x.Debit != 0 || x.Credit != 0).ToArray();
        var result = await journalPosting.PostAsync(new(date, sourceType, sourceId, kind, description, lines), by, ct);
        return result.Succeeded ? OperationResult<bool>.Success(true) : OperationResult<bool>.Failure(result.Errors);
    }

    private async Task<OperationResult<IReadOnlyCollection<JournalLineRequest>>> PaymentLines(
        IEnumerable<AccountingPayment> payments, bool debit, CancellationToken ct)
    {
        var lines = new List<JournalLineRequest>();
        foreach (var payment in payments)
        {
            var account = await PaymentAccount(payment.PaymentMethodId, ct);
            if (!account.Succeeded) return OperationResult<IReadOnlyCollection<JournalLineRequest>>.Failure(account.Errors);
            lines.Add(debit
                ? new(account.Value!.Id, payment.Amount, 0)
                : new(account.Value!.Id, 0, payment.Amount));
        }
        return OperationResult<IReadOnlyCollection<JournalLineRequest>>.Success(lines);
    }

    private async Task AddDebit(List<JournalLineRequest> lines, string key, decimal amount, CancellationToken ct)
    {
        if (FinanceAmount.Round(amount) == 0) return;
        var account = await Require(SystemAccount(key, ct)); lines.Add(new(account.Id, amount, 0));
    }
    private async Task AddCredit(List<JournalLineRequest> lines, string key, decimal amount, CancellationToken ct)
    {
        if (FinanceAmount.Round(amount) == 0) return;
        var account = await Require(SystemAccount(key, ct)); lines.Add(new(account.Id, 0, amount));
    }

    private static async Task<Account> Require(Task<OperationResult<Account>> task)
    {
        var result = await task;
        if (!result.Succeeded) throw new FinanceConfigurationException(result.Errors.First());
        return result.Value!;
    }

    private Task<OperationResult<Account>> SystemAccount(string key, CancellationToken ct) => MappingAccount(
        dbContext.AccountMappings.Where(x => x.Type == AccountMappingType.System && x.Key == key && x.IsActive),
        $"Required Finance mapping '{key}' is missing, inactive or incompatible.", ct);
    private Task<OperationResult<Account>> PaymentAccount(Guid id, CancellationToken ct) => MappingAccount(
        dbContext.AccountMappings.Where(x => x.Type == AccountMappingType.PaymentMethod && x.PaymentMethodId == id && x.IsActive),
        "The selected payment method does not have an active Finance account mapping.", ct);
    private Task<OperationResult<Account>> ExpenseAccount(Guid id, CancellationToken ct) => MappingAccount(
        dbContext.AccountMappings.Where(x => x.Type == AccountMappingType.ExpenseCategory && x.ExpenseCategoryId == id && x.IsActive),
        "An expense category does not have an active Finance account mapping.", ct);

    private static OperationResult<bool> StateResult(OperationResult<PostingState> state) =>
        state.Succeeded ? OperationResult<bool>.Success(false) : OperationResult<bool>.Failure(state.Errors);

    private async Task<OperationResult<Account>> MappingAccount(IQueryable<AccountMapping> query, string error, CancellationToken ct)
    {
        var mapping = await query.Include(x => x.Account).SingleOrDefaultAsync(ct);
        return mapping?.Account is { IsActive: true, Kind: AccountKind.Posting } account
            ? OperationResult<Account>.Success(account)
            : OperationResult<Account>.Failure(error);
    }

    private async Task<OperationResult<PostingState>> State(DateTimeOffset occurredOn, CancellationToken ct)
    {
        var setting = await dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(setting?.TimeZone ?? "Asia/Dhaka");
        return await State(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurredOn, zone).DateTime), ct);
    }
    private async Task<OperationResult<PostingState>> State(DateOnly date, CancellationToken ct)
    {
        var activation = await dbContext.FinanceActivations.AsNoTracking().FirstOrDefaultAsync(ct);
        if (activation is null || !activation.AutomaticPostingEnabled) return OperationResult<PostingState>.Success(new(false, date));
        if (!activation.CutoverDate.HasValue) return OperationResult<PostingState>.Failure("Finance activation has no approved cutover date.");
        return OperationResult<PostingState>.Success(new(date >= activation.CutoverDate.Value, date));
    }

    private sealed record PostingState(bool Enabled, DateOnly Date);
    private sealed class FinanceConfigurationException(string message) : InvalidOperationException(message);
}
