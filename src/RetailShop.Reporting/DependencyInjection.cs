using Microsoft.Extensions.DependencyInjection;

namespace RetailShop.Reporting;

public static class DependencyInjection
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        // Dapper report queries and exporters will be registered here.
        return services;
    }
}
