using Hangfire.Dashboard;
using RetailShop.Application.Security;

namespace RetailShop.Api.Infrastructure;

public sealed class HangfireDashboardAuthorizationFilter
    : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true &&
            httpContext.User.HasPermission(Permissions.Notifications.Manage);
    }
}
