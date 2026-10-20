using Microsoft.EntityFrameworkCore;

namespace RetailShop.Infrastructure.Persistence;

internal sealed class DatabaseInitializer(
    RetailShopDbContext dbContext,
    Identity.IdentitySeeder identitySeeder,
    Application.Finance.IFinanceInitializer financeInitializer) : IDatabaseInitializer
{
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await identitySeeder.SeedAsync(cancellationToken);
        await financeInitializer.InitialiseAsync(cancellationToken);
    }
}
