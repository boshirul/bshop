using Microsoft.Extensions.DependencyInjection;

namespace RetailShop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application use cases and validators will be registered here by module.
        return services;
    }
}
