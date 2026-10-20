using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Finance;
using RetailShop.Domain.Expenses;
using RetailShop.Domain.Finance;
using RetailShop.Domain.Settings;
using RetailShop.Infrastructure.Finance;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Tests.Finance;

public sealed class AutomaticAccountingServiceTests
{
    [Fact]
    public async Task Disabled_finance_never_requires_mappings_or_creates_journals()
    {
        await using var db = Context();
        await new FinanceInitializer(db).InitialiseAsync();
        var service = Service(db);
        var result = await service.PostSaleAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow,
            100m, 0, 50m, [new(Guid.NewGuid(), 100m)]), Guid.NewGuid());
        Assert.True(result.Succeeded); Assert.False(result.Value);
        Assert.Empty(await db.JournalEntries.ToArrayAsync());
    }

    [Fact]
    public async Task Cash_credit_and_mixed_sales_post_revenue_receivable_tax_and_cogs_once()
    {
        await using var db = await Ready();
        var cash = await Payment(db, "CASH", "1110");
        var saleId = Guid.NewGuid();
        var posting = new AutomaticSalePosting(saleId, Occurred(), 115m, 15m, 60m,
            [new(cash.Id, 40m)]);
        var first = await Service(db).PostSaleAsync(posting, Guid.NewGuid());
        var retry = await Service(db).PostSaleAsync(posting, Guid.NewGuid());
        Assert.True(first.Succeeded); Assert.True(retry.Succeeded);
        var journal = await db.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.SourceId == saleId);
        await AssertLine(db, journal, "1110", 40m, 0);
        await AssertLine(db, journal, "1200", 75m, 0);
        await AssertLine(db, journal, "4100", 0, 100m);
        await AssertLine(db, journal, "2300", 0, 15m);
        await AssertLine(db, journal, "5000", 60m, 0);
        await AssertLine(db, journal, "1300", 0, 60m);
    }

    [Fact]
    public async Task Customer_collection_and_supplier_payment_use_control_accounts_without_revenue()
    {
        await using var db = await Ready();
        var cash = await Payment(db, "CASH", "1110");
        Assert.True((await Service(db).PostPaymentAsync(new("CustomerReceipt", Guid.NewGuid(), Occurred(),
            cash.Id, 30m, true), Guid.NewGuid())).Succeeded);
        Assert.True((await Service(db).PostPaymentAsync(new("PurchasePayment", Guid.NewGuid(), Occurred(),
            cash.Id, 20m, false, true), Guid.NewGuid())).Succeeded);
        var journals = await db.JournalEntries.Include(x => x.Lines).ToArrayAsync();
        Assert.Equal(2, journals.Length);
        Assert.DoesNotContain(journals.SelectMany(x => x.Lines), x =>
            db.Accounts.Single(a => a.Id == x.AccountId).Code == "4100");
    }

    [Fact]
    public async Task Purchase_posts_inventory_payments_and_supplier_liability()
    {
        await using var db = await Ready();
        var cash = await Payment(db, "CASH", "1110");
        var id = Guid.NewGuid();
        var result = await Service(db).PostPurchaseAsync(new(id, Occurred(), 100m,
            [new(cash.Id, 25m)]), Guid.NewGuid());
        Assert.True(result.Succeeded);
        var journal = await db.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.SourceId == id);
        await AssertLine(db, journal, "1300", 100m, 0);
        await AssertLine(db, journal, "1110", 0, 25m);
        await AssertLine(db, journal, "2100", 0, 75m);
    }

    [Fact]
    public async Task Expense_requires_category_and_payment_mappings()
    {
        await using var db = await Ready();
        var unmapped = new PaymentMethod("Unmapped", "UNMAPPED", "Cash");
        db.PaymentMethods.Add(unmapped); await db.SaveChangesAsync();
        var missing = await Service(db).PostExpenseAsync(new(Guid.NewGuid(), Date(), unmapped.Id,
            [new(Guid.NewGuid(), 10m)]), Guid.NewGuid());
        Assert.False(missing.Succeeded); Assert.Empty(await db.JournalEntries.ToArrayAsync());

        var cash = await Payment(db, "CASH", "1110");
        var category = new ExpenseCategory("UTIL", "Utilities");
        db.ExpenseCategories.Add(category); await db.SaveChangesAsync();
        var expenseAccount = await db.Accounts.SingleAsync(x => x.Code == "6100");
        db.AccountMappings.Add(new AccountMapping(AccountMappingType.ExpenseCategory, "expense:util",
            expenseAccount.Id, expenseCategoryId: category.Id));
        await db.SaveChangesAsync();
        var posted = await Service(db).PostExpenseAsync(new(Guid.NewGuid(), Date(), cash.Id,
            [new(category.Id, 10m)]), Guid.NewGuid());
        Assert.True(posted.Succeeded);
    }

    [Fact]
    public async Task Sales_and_purchase_returns_reverse_income_tax_cogs_and_supplier_balance()
    {
        await using var db = await Ready();
        var cash = await Payment(db, "CASH", "1110");
        Assert.True((await Service(db).PostSalesReturnAsync(new(Guid.NewGuid(), Occurred(), 115m,
            15m, 40m, 75m, cash.Id, 60m, false), Guid.NewGuid())).Succeeded);
        Assert.True((await Service(db).PostPurchaseReturnAsync(new(Guid.NewGuid(), Occurred(), 50m),
            Guid.NewGuid())).Succeeded);
        Assert.Equal(2, await db.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task Inventory_adjustment_and_reversal_use_dedicated_accounts()
    {
        await using var db = await Ready();
        var id = Guid.NewGuid();
        var service = Service(db);
        Assert.True((await service.PostInventoryAdjustmentAsync(new(id, Occurred(), 30m, 10m),
            Guid.NewGuid())).Succeeded);
        Assert.True((await service.ReverseAsync("StockAdjustment", id, Occurred(), "Correction",
            Guid.NewGuid(), originalOccurredOn: Occurred())).Succeeded);
        Assert.Equal(2, await db.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task Pre_cutover_sources_are_excluded_and_online_sale_source_is_exactly_once()
    {
        await using var db = await Ready();
        var service = Service(db);
        Assert.False((await service.PostSaleAsync(new(Guid.NewGuid(), new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            10m, 0, 5m, []), Guid.NewGuid())).Value);
        var onlineSale = new AutomaticSalePosting(Guid.NewGuid(), Occurred(), 10m, 0, 5m, []);
        Assert.True((await service.PostSaleAsync(onlineSale, Guid.NewGuid())).Succeeded);
        Assert.True((await service.PostSaleAsync(onlineSale, Guid.NewGuid())).Succeeded);
        Assert.Single(await db.JournalEntries.ToArrayAsync());
    }

    private static AutomaticAccountingService Service(RetailShopDbContext db) =>
        new(db, new JournalPostingService(db, TimeProvider.System));
    private static DateOnly Date() => new(2026, 10, 20);
    private static DateTimeOffset Occurred() => new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private static async Task<RetailShopDbContext> Ready()
    {
        var db = Context();
        await new FinanceInitializer(db).InitialiseAsync();
        db.FiscalPeriods.Add(new FiscalPeriod("FY 2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        var activation = await db.FinanceActivations.SingleAsync();
        db.Entry(activation).Property(x => x.AutomaticPostingEnabled).CurrentValue = true;
        db.Entry(activation).Property(x => x.CutoverDate).CurrentValue = new DateOnly(2026, 1, 1);
        db.Entry(activation).Property(x => x.MandatoryMappingsApproved).CurrentValue = true;
        db.Entry(activation).Property(x => x.OpeningBalancesApproved).CurrentValue = true;
        db.Entry(activation).Property(x => x.ReconciliationReady).CurrentValue = true;
        db.Entry(activation).Property(x => x.ApprovedBy).CurrentValue = Guid.NewGuid();
        db.Entry(activation).Property(x => x.ApprovedOn).CurrentValue = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<PaymentMethod> Payment(RetailShopDbContext db, string code, string accountCode)
    {
        var payment = new PaymentMethod(code, code, code);
        db.PaymentMethods.Add(payment); await db.SaveChangesAsync();
        var account = await db.Accounts.SingleAsync(x => x.Code == accountCode);
        db.AccountMappings.Add(new AccountMapping(AccountMappingType.PaymentMethod, $"payment:{code.ToLowerInvariant()}",
            account.Id, payment.Id));
        await db.SaveChangesAsync(); return payment;
    }

    private static async Task AssertLine(RetailShopDbContext db, JournalEntry journal, string code,
        decimal debit, decimal credit)
    {
        var account = await db.Accounts.SingleAsync(x => x.Code == code);
        var line = journal.Lines.Single(x => x.AccountId == account.Id);
        Assert.Equal(debit, line.Debit); Assert.Equal(credit, line.Credit);
    }

    private static RetailShopDbContext Context() => new(new DbContextOptionsBuilder<RetailShopDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
