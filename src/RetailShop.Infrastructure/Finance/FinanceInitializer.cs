using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Finance;
using RetailShop.Domain.Finance;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Finance;

internal sealed class FinanceInitializer(RetailShopDbContext dbContext) : IFinanceInitializer
{
    private sealed record Template(string Code, string Name, AccountType Type, NormalBalance Balance,
        AccountKind Kind, string? ParentCode, bool System, string? MappingKey = null);

    private static readonly Template[] Templates =
    [
        new("1000", "Assets", AccountType.Asset, NormalBalance.Debit, AccountKind.Group, null, true),
        new("1100", "Cash and Cash Equivalents", AccountType.Asset, NormalBalance.Debit, AccountKind.Group, "1000", true),
        new("1110", "Cash in Hand", AccountType.Asset, NormalBalance.Debit, AccountKind.Posting, "1100", true, SystemAccountKeys.CashInHand),
        new("1120", "bKash / Mobile Wallet", AccountType.Asset, NormalBalance.Debit, AccountKind.Posting, "1100", true, SystemAccountKeys.Bkash),
        new("1130", "Bank Accounts", AccountType.Asset, NormalBalance.Debit, AccountKind.Posting, "1100", true, SystemAccountKeys.Bank),
        new("1140", "Card Settlement Clearing", AccountType.Asset, NormalBalance.Debit, AccountKind.Posting, "1100", true, SystemAccountKeys.CardClearing),
        new("1200", "Customer Receivables", AccountType.Asset, NormalBalance.Debit, AccountKind.Posting, "1000", true, SystemAccountKeys.CustomerReceivables),
        new("1300", "Inventory Assets", AccountType.Asset, NormalBalance.Debit, AccountKind.Posting, "1000", true, SystemAccountKeys.InventoryAsset),
        new("2000", "Liabilities", AccountType.Liability, NormalBalance.Credit, AccountKind.Group, null, true),
        new("2100", "Supplier Payables", AccountType.Liability, NormalBalance.Credit, AccountKind.Posting, "2000", true, SystemAccountKeys.SupplierPayables),
        new("2200", "Customer Advances", AccountType.Liability, NormalBalance.Credit, AccountKind.Posting, "2000", true, SystemAccountKeys.CustomerAdvances),
        new("2300", "Tax Payable", AccountType.Liability, NormalBalance.Credit, AccountKind.Posting, "2000", true, SystemAccountKeys.TaxPayable),
        new("3000", "Equity", AccountType.Equity, NormalBalance.Credit, AccountKind.Group, null, true),
        new("3100", "Owner Capital", AccountType.Equity, NormalBalance.Credit, AccountKind.Posting, "3000", true, SystemAccountKeys.OwnerCapital),
        new("3200", "Retained Earnings", AccountType.Equity, NormalBalance.Credit, AccountKind.Posting, "3000", true, SystemAccountKeys.RetainedEarnings),
        new("3300", "Opening Balance Equity", AccountType.Equity, NormalBalance.Credit, AccountKind.Posting, "3000", true, SystemAccountKeys.OpeningBalanceEquity),
        new("4000", "Income", AccountType.Income, NormalBalance.Credit, AccountKind.Group, null, true),
        new("4100", "Sales Revenue", AccountType.Income, NormalBalance.Credit, AccountKind.Posting, "4000", true, SystemAccountKeys.SalesRevenue),
        new("4200", "Sales Returns and Allowances", AccountType.Income, NormalBalance.Debit, AccountKind.Posting, "4000", true, SystemAccountKeys.SalesReturns),
        new("4300", "Inventory Adjustment Gains", AccountType.Income, NormalBalance.Credit, AccountKind.Posting, "4000", true, SystemAccountKeys.InventoryAdjustmentGain),
        new("5000", "Cost of Goods Sold", AccountType.Expense, NormalBalance.Debit, AccountKind.Posting, null, true, SystemAccountKeys.CostOfGoodsSold),
        new("6000", "Operating Expenses", AccountType.Expense, NormalBalance.Debit, AccountKind.Group, null, true),
        new("6100", "General Operating Expense", AccountType.Expense, NormalBalance.Debit, AccountKind.Posting, "6000", false, SystemAccountKeys.OperatingExpenses),
        new("6200", "Inventory Write-off", AccountType.Expense, NormalBalance.Debit, AccountKind.Posting, "6000", true, SystemAccountKeys.InventoryWriteOff)
    ];

    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(ct)
            : null;
        if (dbContext.Database.IsNpgsql())
            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(946207311);", ct);

        var accounts = await dbContext.Accounts.IgnoreQueryFilters().ToDictionaryAsync(x => x.Code, ct);
        foreach (var template in Templates)
        {
            if (accounts.ContainsKey(template.Code)) continue;
            Guid? parentId = template.ParentCode is null ? null : accounts[template.ParentCode].Id;
            var account = new Account(template.Code, template.Name, template.Type, template.Balance,
                template.Kind, parentId, template.System);
            dbContext.Accounts.Add(account);
            accounts.Add(template.Code, account);
        }
        await dbContext.SaveChangesAsync(ct);

        var existingKeys = await dbContext.AccountMappings.IgnoreQueryFilters()
            .Where(x => x.Type == AccountMappingType.System).Select(x => x.Key).ToHashSetAsync(ct);
        foreach (var template in Templates.Where(x => x.MappingKey is not null))
            if (existingKeys.Add(template.MappingKey!))
                dbContext.AccountMappings.Add(new AccountMapping(AccountMappingType.System,
                    template.MappingKey!, accounts[template.Code].Id));

        if (!await dbContext.FinanceActivations.AnyAsync(ct))
            dbContext.FinanceActivations.Add(new FinanceActivation());

        await EnsureFinancialAccountAsync("CASH", "Cash in Hand", FinancialAccountType.Cash, accounts["1110"].Id, ct);
        await EnsureFinancialAccountAsync("BKASH", "bKash", FinancialAccountType.MobileWallet, accounts["1120"].Id, ct);
        await dbContext.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    private async Task EnsureFinancialAccountAsync(string code, string name, FinancialAccountType type,
        Guid accountId, CancellationToken ct)
    {
        if (!await dbContext.FinancialAccounts.IgnoreQueryFilters().AnyAsync(x => x.Code == code, ct))
            dbContext.FinancialAccounts.Add(new FinancialAccount(code, name, type, accountId));
    }
}
