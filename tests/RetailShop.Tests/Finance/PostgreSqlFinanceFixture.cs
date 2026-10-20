using Microsoft.EntityFrameworkCore;
using Npgsql;
using RetailShop.Infrastructure.Finance;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Tests.Finance;

public sealed class PostgreSqlFinanceFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "BSHOP_TEST_POSTGRES_ADMIN";
    public const string AuthorizationVariable = "BSHOP_TEST_POSTGRES_ALLOW_CREATE";

    public PostgreSqlFinanceFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)) ||
            !string.Equals(Environment.GetEnvironmentVariable(AuthorizationVariable), "true", StringComparison.OrdinalIgnoreCase))
            Skip = $"Requires {ConnectionVariable} and explicit {AuthorizationVariable}=true authorization for a disposable database.";
    }
}

public sealed class PostgreSqlFinanceFixture : IAsyncLifetime
{
    private string? adminConnection;
    private string? databaseName;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(PostgreSqlFinanceFactAttribute.ConnectionVariable)
            ?? throw new InvalidOperationException("PostgreSQL test administrator connection is not configured.");
        if (!string.Equals(Environment.GetEnvironmentVariable(PostgreSqlFinanceFactAttribute.AuthorizationVariable),
                "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Disposable database creation was not explicitly authorized.");

        var builder = new NpgsqlConnectionStringBuilder(configured);
        if (string.Equals(builder.Database, "khanshop", StringComparison.OrdinalIgnoreCase) ||
            builder.Database?.Contains("prod", StringComparison.OrdinalIgnoreCase) == true ||
            builder.Database?.Contains("uat", StringComparison.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException("The PostgreSQL integration-test administrator connection points to a protected database name.");

        builder.Database = string.IsNullOrWhiteSpace(builder.Database) ? "postgres" : builder.Database;
        adminConnection = builder.ConnectionString;
        databaseName = $"bshop_finance_test_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(adminConnection))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        builder.Database = databaseName;
        builder.Pooling = false;
        ConnectionString = builder.ConnectionString;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await new FinanceInitializer(db).InitialiseAsync();
    }

    public RetailShopDbContext CreateContext() => new(new DbContextOptionsBuilder<RetailShopDbContext>()
        .UseNpgsql(ConnectionString).Options);

    public async Task DisposeAsync()
    {
        if (adminConnection is null || databaseName is null) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
