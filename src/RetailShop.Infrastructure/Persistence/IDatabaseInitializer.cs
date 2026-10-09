namespace RetailShop.Infrastructure.Persistence;

public interface IDatabaseInitializer
{
    Task InitialiseAsync(CancellationToken cancellationToken = default);
}
