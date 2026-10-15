using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Fonts;

namespace RetailShop.Reporting;

public static class DependencyInjection
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        // Reports run on Windows in the supported deployment target. This enables
        // the platform resolver instead of accepting a user-provided font path.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        services.AddSingleton<IManagementReportExporter, ManagementReportExporter>();
        return services;
    }
}
