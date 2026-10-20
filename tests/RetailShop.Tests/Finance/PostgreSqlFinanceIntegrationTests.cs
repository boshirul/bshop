using Microsoft.EntityFrameworkCore;
using Npgsql;
using RetailShop.Application.Common;
using RetailShop.Application.Finance;
using RetailShop.Domain.Finance;
using RetailShop.Infrastructure.Finance;

namespace RetailShop.Tests.Finance;

public sealed class PostgreSqlFinanceIntegrationTests(PostgreSqlFinanceFixture fixture)
    : IClassFixture<PostgreSqlFinanceFixture>
{
    private static int periodSequence;

    [PostgreSqlFinanceFact]
    public async Task Full_migration_chain_creates_expected_finance_integrity_objects()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        var migrations = await ScalarArray(connection,
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
        Assert.Contains("20261016025857_OnlineOrderSaleIntegration", migrations);
        Assert.Contains("20261016180000_OnlineCheckoutIdempotency", migrations);
        Assert.Contains("20261020022459_FinanceAccountingFoundation", migrations);

        var triggers = await ScalarArray(connection,
            "SELECT tgname FROM pg_trigger WHERE NOT tgisinternal AND tgrelid IN ('\"JournalEntries\"'::regclass, '\"JournalLines\"'::regclass)");
        Assert.Contains("TR_JournalEntries_Balanced", triggers);
        Assert.Contains("TR_JournalEntries_Immutable", triggers);
        Assert.Contains("TR_JournalLines_ValidateInsert", triggers);
        Assert.Contains("TR_JournalLines_Balanced", triggers);

        var exclusion = await Scalar<long>(connection,
            "SELECT COUNT(*) FROM pg_constraint WHERE conname = 'EX_FiscalPeriods_NoOverlap'");
        Assert.Equal(1, exclusion);
        Assert.Equal(0, await Scalar<long>(connection, "SELECT COUNT(*) FROM \"Sales\""));
        Assert.Equal(0, await Scalar<long>(connection, "SELECT COUNT(*) FROM \"JournalEntries\""));
    }

    [PostgreSqlFinanceFact]
    public async Task Service_posts_balanced_multiline_and_database_rejects_invalid_journals()
    {
        var setup = await SetupPostingAsync();
        await using (var db = fixture.CreateContext())
        {
            var service = new JournalPostingService(db, TimeProvider.System);
            var result = await service.PostAsync(new PostJournalRequest(setup.Date, "PgBalance", Guid.NewGuid(),
                FinancePostingKinds.Manual, "Multiline",
                [new(setup.Debit, 6.005m, 0m), new(setup.Debit, 4.004m, 0m), new(setup.Credit, 0m, 10.01m)]), Guid.NewGuid());
            Assert.True(result.Succeeded, string.Join("; ", result.Errors));
            Assert.Equal(10.01m, result.Value!.Lines.Sum(x => x.Debit));
        }

        var badSource = Guid.NewGuid();
        await using (var db = fixture.CreateContext())
        {
            var period = await db.FiscalPeriods.SingleAsync(x => x.Contains(setup.Date));
            var journal = new JournalEntry($"BAD-{Guid.NewGuid():N}"[..23], setup.Date, DateTimeOffset.UtcNow,
                "PgDirect", badSource, "Bad", new string('A', 64), null, Guid.NewGuid(), period.Id, null);
            journal.AddLine(new JournalLine(setup.Debit, 10m, 0m, null));
            db.JournalEntries.Add(journal);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var verify = fixture.CreateContext())
            Assert.False(await verify.JournalEntries.AnyAsync(x => x.SourceId == badSource));
    }

    [PostgreSqlFinanceFact]
    public async Task Concurrent_identical_and_conflicting_sources_are_exactly_once_and_context_recovers()
    {
        var setup = await SetupPostingAsync();
        var source = Guid.NewGuid();
        var request = Request(setup, source, 20m);
        var results = await Task.WhenAll(PostNewContext(request), PostNewContext(request));
        Assert.All(results, x => Assert.True(x.Succeeded, string.Join("; ", x.Errors)));
        Assert.Equal(results[0].Value!.Id, results[1].Value!.Id);

        var conflictSource = Guid.NewGuid();
        var conflicts = await Task.WhenAll(
            PostNewContext(Request(setup, conflictSource, 30m)),
            PostNewContext(Request(setup, conflictSource, 31m)));
        Assert.Single(conflicts, x => x.Succeeded);
        Assert.Single(conflicts, x => !x.Succeeded);

        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.JournalEntries.CountAsync(x => x.SourceId == source));
        Assert.Equal(1, await db.JournalEntries.CountAsync(x => x.SourceId == conflictSource));
        var service = new JournalPostingService(db, TimeProvider.System);
        Assert.False((await service.PostAsync(Request(setup, conflictSource, 99m), Guid.NewGuid())).Succeeded);
        Assert.True((await service.PostAsync(Request(setup, Guid.NewGuid(), 1m), Guid.NewGuid())).Succeeded);
    }

    [PostgreSqlFinanceFact]
    public async Task Outer_transaction_controls_commit_and_rollback()
    {
        var setup = await SetupPostingAsync();
        var rolledBackSource = Guid.NewGuid();
        await using (var db = fixture.CreateContext())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var result = await new JournalPostingService(db, TimeProvider.System)
                .PostAsync(Request(setup, rolledBackSource, 5m), Guid.NewGuid());
            Assert.True(result.Succeeded);
            Assert.True(await db.JournalEntries.AnyAsync(x => x.SourceId == rolledBackSource));
            await transaction.RollbackAsync();
        }
        await using var verify = fixture.CreateContext();
        Assert.False(await verify.JournalEntries.AnyAsync(x => x.SourceId == rolledBackSource));
        Assert.False(await verify.JournalLines.AnyAsync(x => x.JournalEntry.SourceId == rolledBackSource));
    }

    [PostgreSqlFinanceFact]
    public async Task Direct_mutation_line_insertion_and_account_identity_changes_are_rejected()
    {
        var setup = await SetupPostingAsync();
        var posted = await PostNewContext(Request(setup, Guid.NewGuid(), 8m));
        Assert.True(posted.Succeeded);
        var postedJournal = Assert.IsType<JournalItem>(posted.Value);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
            "UPDATE \"JournalEntries\" SET \"Description\"='tampered' WHERE \"Id\"=@id", postedJournal.Id));
        await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
            "UPDATE \"JournalLines\" SET \"Debit\"=9 WHERE \"Id\"=@id", postedJournal.Lines.First().Id));
        await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
            "INSERT INTO \"JournalLines\" (\"Id\",\"JournalEntryId\",\"AccountId\",\"Debit\",\"Credit\") VALUES (@newId,@id,@account,1,0)",
            postedJournal.Id, Guid.NewGuid(), setup.Debit));
        await Assert.ThrowsAsync<PostgresException>(() => Execute(connection,
            "UPDATE \"FinanceAccounts\" SET \"Code\"='TAMPERED' WHERE \"Id\"=@id", setup.Debit));
    }

    [PostgreSqlFinanceFact]
    public async Task Reversal_is_atomic_immutable_and_only_once()
    {
        var setup = await SetupPostingAsync();
        var original = await PostNewContext(Request(setup, Guid.NewGuid(), 17m));
        await using var db = fixture.CreateContext();
        var service = new JournalPostingService(db, TimeProvider.System);
        var reversal = await service.ReverseAsync(original.Value!.Id, setup.Date, "PostgreSQL reversal", Guid.NewGuid());
        Assert.True(reversal.Succeeded, string.Join("; ", reversal.Errors));
        Assert.Equal(original.Value.Id, reversal.Value!.ReversesJournalId);
        Assert.False((await service.ReverseAsync(original.Value.Id, setup.Date, "Duplicate", Guid.NewGuid())).Succeeded);
        Assert.Equal(2, await db.JournalEntries.CountAsync(x => x.Id == original.Value.Id || x.ReversesJournalId == original.Value.Id));
    }

    [PostgreSqlFinanceFact]
    public async Task Fiscal_exclusion_closed_policy_and_concurrent_overlap_are_enforced()
    {
        var year = 2200 + Interlocked.Increment(ref periodSequence);
        var first = new FiscalPeriod($"PG-{year}", new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        await using (var db = fixture.CreateContext())
        {
            db.FiscalPeriods.Add(first); await db.SaveChangesAsync();
            first.ChangeStatus(FiscalPeriodStatus.Closed, Guid.NewGuid(), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        var accountIds = await AccountIds();
        await using (var db = fixture.CreateContext())
        {
            var rejected = await new JournalPostingService(db, TimeProvider.System).PostAsync(
                new PostJournalRequest(new DateOnly(year, 1, 1), "Closed", Guid.NewGuid(), "Test", null,
                    [new(accountIds.Debit, 1m, 0m), new(accountIds.Credit, 0m, 1m)]), Guid.NewGuid());
            Assert.False(rejected.Succeeded);
        }

        var overlapYear = year + 100;
        async Task<bool> Create(DateOnly start, DateOnly end)
        {
            await using var db = fixture.CreateContext();
            return (await new FinanceAdministrationService(db).CreateFiscalPeriodAsync(
                new FiscalPeriod(Guid.NewGuid().ToString("N"), start, end))).Succeeded;
        }
        var concurrent = await Task.WhenAll(
            Create(new DateOnly(overlapYear, 1, 1), new DateOnly(overlapYear, 12, 31)),
            Create(new DateOnly(overlapYear, 6, 1), new DateOnly(overlapYear + 1, 5, 31)));
        Assert.Single(concurrent, x => x);
    }

    [PostgreSqlFinanceFact]
    public async Task Initializer_is_concurrency_safe_preserves_customization_and_activation_stays_disabled()
    {
        await using (var customize = fixture.CreateContext())
        {
            var cash = await customize.Accounts.SingleAsync(x => x.Code == "1110");
            cash.Rename("Customized Cash Till");
            await customize.SaveChangesAsync();
        }
        async Task RunInitializer()
        {
            await using var db = fixture.CreateContext();
            await new FinanceInitializer(db).InitialiseAsync();
        }
        await Task.WhenAll(RunInitializer(), RunInitializer());
        await using var verify = fixture.CreateContext();
        Assert.Equal(24, await verify.Accounts.CountAsync());
        Assert.Equal(18, await verify.AccountMappings.CountAsync(x => x.Type == AccountMappingType.System));
        Assert.Equal("Customized Cash Till", (await verify.Accounts.SingleAsync(x => x.Code == "1110")).Name);
        Assert.False((await verify.FinanceActivations.SingleAsync()).AutomaticPostingEnabled);
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await verify.Database.ExecuteSqlRawAsync("UPDATE \"FinanceActivations\" SET \"AutomaticPostingEnabled\"=TRUE");
        });
    }

    private async Task<(Guid Debit, Guid Credit, DateOnly Date)> SetupPostingAsync()
    {
        var ids = await AccountIds();
        var year = 2050 + Interlocked.Increment(ref periodSequence);
        var date = new DateOnly(year, 6, 30);
        await using var db = fixture.CreateContext();
        db.FiscalPeriods.Add(new FiscalPeriod($"Test {year}", new DateOnly(year, 1, 1), new DateOnly(year, 12, 31)));
        await db.SaveChangesAsync();
        return (ids.Debit, ids.Credit, date);
    }

    private async Task<(Guid Debit, Guid Credit)> AccountIds()
    {
        await using var db = fixture.CreateContext();
        var debit = await db.Accounts.SingleAsync(x => x.Code == "1110");
        var credit = await db.Accounts.SingleAsync(x => x.Code == "4100");
        return (debit.Id, credit.Id);
    }

    private static PostJournalRequest Request((Guid Debit, Guid Credit, DateOnly Date) setup, Guid source, decimal amount) =>
        new(setup.Date, "PgConcurrency", source, FinancePostingKinds.Manual, "Concurrency",
            [new(setup.Debit, amount, 0m), new(setup.Credit, 0m, amount)]);

    private async Task<OperationResult<JournalItem>> PostNewContext(PostJournalRequest request)
    {
        await using var db = fixture.CreateContext();
        return await new JournalPostingService(db, TimeProvider.System).PostAsync(request, Guid.NewGuid());
    }

    private static async Task Execute(NpgsqlConnection connection, string sql, Guid id, Guid? newId = null, Guid? account = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        if (newId.HasValue) command.Parameters.AddWithValue("newId", newId.Value);
        if (account.HasValue) command.Parameters.AddWithValue("account", account.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> Scalar<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }

    private static async Task<string[]> ScalarArray(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values.ToArray();
    }
}
