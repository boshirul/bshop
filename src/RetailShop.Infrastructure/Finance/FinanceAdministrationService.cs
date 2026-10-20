using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Common;
using RetailShop.Application.Finance;
using RetailShop.Domain.Finance;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Finance;

internal sealed class FinanceAdministrationService(RetailShopDbContext dbContext)
    : IFinanceAdministrationService
{
    public async Task<OperationResult<Account>> CreateAccountAsync(Account account, CancellationToken ct = default)
    {
        if (await dbContext.Accounts.AnyAsync(x => x.Code == account.Code, ct))
            return OperationResult<Account>.Failure("An account with this code already exists.");
        if (account.ParentId is Guid parentId)
        {
            var parent = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == parentId, ct);
            if (parent is null) return OperationResult<Account>.Failure("Parent account was not found.");
            try { account.AssignParent(parent); }
            catch (ArgumentException ex) { return OperationResult<Account>.Failure(ex.Message); }
        }
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(ct);
        return OperationResult<Account>.Success(account);
    }

    public async Task<OperationResult<FiscalPeriod>> CreateFiscalPeriodAsync(FiscalPeriod period, CancellationToken ct = default)
    {
        var overlaps = await dbContext.FiscalPeriods.AnyAsync(x =>
            x.StartDate <= period.EndDate && period.StartDate <= x.EndDate, ct);
        if (overlaps) return OperationResult<FiscalPeriod>.Failure("Fiscal periods cannot overlap.");
        dbContext.FiscalPeriods.Add(period);
        try { await dbContext.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            dbContext.Entry(period).State = EntityState.Detached;
            return OperationResult<FiscalPeriod>.Failure("Fiscal periods cannot overlap.");
        }
        return OperationResult<FiscalPeriod>.Success(period);
    }

    public async Task<OperationResult<FinancialAccount>> CreateFinancialAccountAsync(FinancialAccount financial, CancellationToken ct = default)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == financial.AccountId, ct);
        var error = ValidatePostingAsset(account);
        if (error is not null) return OperationResult<FinancialAccount>.Failure(error);
        if (await dbContext.FinancialAccounts.AnyAsync(x => x.Code == financial.Code, ct))
            return OperationResult<FinancialAccount>.Failure("A financial account with this code already exists.");
        dbContext.FinancialAccounts.Add(financial);
        await dbContext.SaveChangesAsync(ct);
        return OperationResult<FinancialAccount>.Success(financial);
    }

    public async Task<OperationResult<AccountMapping>> SaveMappingAsync(AccountMapping mapping, CancellationToken ct = default)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == mapping.AccountId, ct);
        if (account is null || !account.IsActive || account.Kind != AccountKind.Posting)
            return OperationResult<AccountMapping>.Failure("Mapping requires an active posting account.");
        if (mapping.Type == AccountMappingType.PaymentMethod && account.Type != AccountType.Asset)
            return OperationResult<AccountMapping>.Failure("Payment methods require an Asset posting account.");
        if (mapping.Type == AccountMappingType.ExpenseCategory && account.Type != AccountType.Expense)
            return OperationResult<AccountMapping>.Failure("Expense categories require an Expense posting account.");
        if (mapping.Type == AccountMappingType.PaymentMethod && mapping.PaymentMethodId is null ||
            mapping.Type == AccountMappingType.ExpenseCategory && mapping.ExpenseCategoryId is null ||
            mapping.Type == AccountMappingType.System && (mapping.PaymentMethodId is not null || mapping.ExpenseCategoryId is not null))
            return OperationResult<AccountMapping>.Failure("Mapping type and target are incompatible.");
        if (mapping.PaymentMethodId is Guid paymentId &&
            !await dbContext.PaymentMethods.AnyAsync(x => x.Id == paymentId && x.IsActive, ct))
            return OperationResult<AccountMapping>.Failure("Payment method was not found or is inactive.");
        if (mapping.ExpenseCategoryId is Guid categoryId &&
            !await dbContext.ExpenseCategories.AnyAsync(x => x.Id == categoryId && x.IsActive, ct))
            return OperationResult<AccountMapping>.Failure("Expense category was not found or is inactive.");
        if (await dbContext.AccountMappings.AnyAsync(x => x.Type == mapping.Type && x.Key == mapping.Key, ct))
            return OperationResult<AccountMapping>.Failure("This account mapping already exists.");
        dbContext.AccountMappings.Add(mapping);
        await dbContext.SaveChangesAsync(ct);
        return OperationResult<AccountMapping>.Success(mapping);
    }

    private static string? ValidatePostingAsset(Account? account) => account switch
    {
        null => "GL account was not found.",
        { IsActive: false } => "GL account is inactive.",
        { Kind: not AccountKind.Posting } => "Financial accounts require a posting GL account.",
        { Type: not AccountType.Asset } => "Financial accounts require an Asset GL account.",
        _ => null
    };
}
