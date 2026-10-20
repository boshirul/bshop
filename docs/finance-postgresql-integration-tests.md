# Finance PostgreSQL integration tests

The Finance integration suite creates and later drops its own uniquely named
`bshop_finance_test_<guid>` database. It never migrates the database named by
the application configuration.

The suite is disabled unless both variables below are explicitly supplied:

```powershell
$env:BSHOP_TEST_POSTGRES_ADMIN = 'Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=<test-only-password>'
$env:BSHOP_TEST_POSTGRES_ALLOW_CREATE = 'true'
dotnet test tests/RetailShop.Tests/RetailShop.Tests.csproj --filter FullyQualifiedName~PostgreSqlFinanceIntegrationTests
```

Use a disposable PostgreSQL server or a dedicated test server where the test
login may create and drop databases. The fixture refuses administrator
connections whose configured database name contains `khanshop`, `prod`, or
`uat`. Do not point these variables at the IIS business database.

The complete EF migration chain is applied to the generated database. The
fixture clears Npgsql pools and drops only the database name that it generated.
