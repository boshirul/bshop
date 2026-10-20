using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Finance;
using RetailShop.Domain.Finance;
using RetailShop.Infrastructure.Finance;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Tests.Finance;

public sealed class FinanceFoundationTests
{
    [Fact]
    public void Account_validates_code_and_hierarchy()
    {
        Assert.Throws<ArgumentException>(() => new Account("!", "Bad", AccountType.Asset,
            NormalBalance.Debit, AccountKind.Posting));
        var asset = Group("1000", AccountType.Asset);
        var liability = Group("2000", AccountType.Liability);
        var cash = Posting("1110", AccountType.Asset);
        Assert.Throws<ArgumentException>(() => cash.AssignParent(liability));
        cash.AssignParent(asset);
        Assert.Equal(asset.Id, cash.ParentId);
    }

    [Fact]
    public void Account_rejects_circular_hierarchy()
    {
        var first = Group("1000", AccountType.Asset);
        var second = Group("1100", AccountType.Asset);
        second.AssignParent(first);
        Assert.Throws<ArgumentException>(() => first.AssignParent(second));
    }

    [Fact]
    public async Task Administration_rejects_duplicate_codes_and_overlapping_periods()
    {
        await using var db = Context();
        var service = new FinanceAdministrationService(db);
        Assert.True((await service.CreateAccountAsync(Posting("1110", AccountType.Asset))).Succeeded);
        Assert.False((await service.CreateAccountAsync(Posting("1110", AccountType.Asset))).Succeeded);
        Assert.True((await service.CreateFiscalPeriodAsync(Period())).Succeeded);
        Assert.False((await service.CreateFiscalPeriodAsync(
            new FiscalPeriod("Overlap", new DateOnly(2026, 6, 1), new DateOnly(2027, 5, 31)))).Succeeded);
    }

    [Fact]
    public async Task Posting_rounds_four_decimal_sources_and_posts_balanced_journal()
    {
        await using var db = await ReadyContext();
        var accounts = await db.Accounts.Where(x => x.Kind == AccountKind.Posting).Take(2).ToArrayAsync();
        var result = await Service(db).PostAsync(Request(accounts[0].Id, accounts[1].Id, 10.005m), Guid.NewGuid());
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(10.01m, result.Value!.Lines.Sum(x => x.Debit));
        Assert.Equal(result.Value.Lines.Sum(x => x.Debit), result.Value.Lines.Sum(x => x.Credit));
    }

    [Fact]
    public async Task Posting_rejects_unbalanced_invalid_and_restricted_lines()
    {
        await using var db = await ReadyContext();
        var posting = await db.Accounts.FirstAsync(x => x.Kind == AccountKind.Posting);
        var group = await db.Accounts.FirstAsync(x => x.Kind == AccountKind.Group);
        var service = Service(db);
        var source = Guid.NewGuid();
        var unbalanced = await service.PostAsync(new PostJournalRequest(Date(), "Test", source, "Test", null,
            [new(posting.Id, 10m, 0m), new(posting.Id, 0m, 9m)]), Guid.NewGuid());
        var both = await service.PostAsync(new PostJournalRequest(Date(), "Test", Guid.NewGuid(), "Test", null,
            [new(posting.Id, 1m, 1m), new(posting.Id, 0m, 1m)]), Guid.NewGuid());
        var grouped = await service.PostAsync(Request(group.Id, posting.Id, 1m), Guid.NewGuid());
        Assert.False(unbalanced.Succeeded); Assert.False(both.Succeeded); Assert.False(grouped.Succeeded);
        Assert.Empty(await db.JournalEntries.ToArrayAsync());
    }

    [Fact]
    public async Task Posting_rejects_closed_period()
    {
        await using var db = await ReadyContext();
        var period = await db.FiscalPeriods.SingleAsync();
        period.ChangeStatus(FiscalPeriodStatus.Closed, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        var accounts = await db.Accounts.Where(x => x.Kind == AccountKind.Posting).Take(2).ToArrayAsync();
        Assert.False((await Service(db).PostAsync(Request(accounts[0].Id, accounts[1].Id, 1m), Guid.NewGuid())).Succeeded);
    }

    [Fact]
    public async Task Identical_retry_returns_existing_but_conflicting_duplicate_is_rejected()
    {
        await using var db = await ReadyContext();
        var accounts = await db.Accounts.Where(x => x.Kind == AccountKind.Posting).Take(2).ToArrayAsync();
        var request = Request(accounts[0].Id, accounts[1].Id, 12m);
        var first = await Service(db).PostAsync(request, Guid.NewGuid());
        var retry = await Service(db).PostAsync(request, Guid.NewGuid());
        var conflict = await Service(db).PostAsync(request with
        {
            Lines = [new(accounts[0].Id, 13m, 0m), new(accounts[1].Id, 0m, 13m)]
        }, Guid.NewGuid());
        Assert.True(first.Succeeded); Assert.True(retry.Succeeded); Assert.Equal(first.Value!.Id, retry.Value!.Id);
        Assert.False(conflict.Succeeded); Assert.Single(await db.JournalEntries.ToArrayAsync());
    }

    [Fact]
    public async Task Reversal_is_distinct_balanced_and_allowed_once()
    {
        await using var db = await ReadyContext();
        var accounts = await db.Accounts.Where(x => x.Kind == AccountKind.Posting).Take(2).ToArrayAsync();
        var original = await Service(db).PostAsync(Request(accounts[0].Id, accounts[1].Id, 25m), Guid.NewGuid());
        var reversal = await Service(db).ReverseAsync(original.Value!.Id, Date(), "Correction", Guid.NewGuid());
        var duplicate = await Service(db).ReverseAsync(original.Value.Id, Date(), "Again", Guid.NewGuid());
        Assert.True(reversal.Succeeded); Assert.Equal(original.Value.Id, reversal.Value!.ReversesJournalId);
        Assert.Equal(original.Value.Lines.Single(x => x.Debit > 0).Debit,
            reversal.Value.Lines.Single(x => x.Credit > 0).Credit);
        Assert.False(duplicate.Succeeded); Assert.Equal(2, await db.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task Posted_journal_and_account_identity_are_immutable()
    {
        await using var db = await ReadyContext();
        var accounts = await db.Accounts.Where(x => x.Kind == AccountKind.Posting).Take(2).ToArrayAsync();
        var posted = await Service(db).PostAsync(Request(accounts[0].Id, accounts[1].Id, 5m), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => accounts[0].ChangeIdentity("9999", AccountType.Asset,
            NormalBalance.Debit, AccountKind.Posting, null));
        var journal = await db.JournalEntries.SingleAsync(x => x.Id == posted.Value!.Id);
        db.Entry(journal).Property(x => x.Description).CurrentValue = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Mapping_and_financial_accounts_validate_targets()
    {
        await using var db = Context();
        var service = new FinanceAdministrationService(db);
        var income = Posting("4100", AccountType.Income);
        db.Accounts.Add(income); await db.SaveChangesAsync();
        Assert.False((await service.CreateFinancialAccountAsync(
            new FinancialAccount("BANK", "Bank", FinancialAccountType.Bank, income.Id))).Succeeded);
        Assert.False((await service.SaveMappingAsync(
            new AccountMapping(AccountMappingType.System, "missing", Guid.NewGuid()))).Succeeded);
    }

    [Fact]
    public async Task Initialisation_is_idempotent_and_keeps_automatic_posting_disabled()
    {
        await using var db = Context();
        var initializer = new FinanceInitializer(db);
        await initializer.InitialiseAsync();
        var accountCount = await db.Accounts.CountAsync();
        var mappingCount = await db.AccountMappings.CountAsync();
        await initializer.InitialiseAsync();
        Assert.Equal(accountCount, await db.Accounts.CountAsync());
        Assert.Equal(mappingCount, await db.AccountMappings.CountAsync());
        Assert.False((await db.FinanceActivations.SingleAsync()).AutomaticPostingEnabled);
        Assert.Empty(await db.JournalEntries.ToArrayAsync());
    }

    [Fact]
    public async Task Rejected_posting_leaves_no_partial_journal_or_lines()
    {
        await using var db = await ReadyContext();
        var account = await db.Accounts.FirstAsync(x => x.Kind == AccountKind.Posting);
        var result = await Service(db).PostAsync(Request(account.Id, Guid.NewGuid(), 10m), Guid.NewGuid());
        Assert.False(result.Succeeded);
        Assert.Empty(await db.JournalEntries.ToArrayAsync());
        Assert.Empty(await db.JournalLines.ToArrayAsync());
    }

    private static JournalPostingService Service(RetailShopDbContext db) => new(db, TimeProvider.System);
    private static DateOnly Date() => new(2026, 10, 20);
    private static FiscalPeriod Period() => new("FY 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
    private static Account Group(string code, AccountType type) => new(code, code, type,
        type is AccountType.Asset or AccountType.Expense ? NormalBalance.Debit : NormalBalance.Credit, AccountKind.Group);
    private static Account Posting(string code, AccountType type) => new(code, code, type,
        type is AccountType.Asset or AccountType.Expense ? NormalBalance.Debit : NormalBalance.Credit, AccountKind.Posting);
    private static PostJournalRequest Request(Guid debit, Guid credit, decimal amount) =>
        new(Date(), "SyntheticFinanceTest", Guid.NewGuid(), FinancePostingKinds.Manual, "Test",
            [new(debit, amount, 0m), new(credit, 0m, amount)]);

    private static RetailShopDbContext Context() => new(new DbContextOptionsBuilder<RetailShopDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<RetailShopDbContext> ReadyContext()
    {
        var db = Context();
        db.FiscalPeriods.Add(Period());
        db.Accounts.AddRange(Group("1000", AccountType.Asset), Posting("1110", AccountType.Asset), Posting("4100", AccountType.Income));
        await db.SaveChangesAsync();
        return db;
    }
}
