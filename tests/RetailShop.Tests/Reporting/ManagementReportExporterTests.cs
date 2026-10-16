using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf.IO;
using RetailShop.Application.Reports;
using RetailShop.Application.Settings;
using RetailShop.Reporting;

namespace RetailShop.Tests.Reporting;

public sealed class ManagementReportExporterTests
{
    [Fact]
    public void ExportPdf_UsesReadableCurrencyFallbackAndCreatesMultiplePagesForLongReports()
    {
        var report = CreateReport(rowCount: 100);
        var file = CreateExporter().ExportPdf(report, Shop());

        Assert.Equal("application/pdf", file.ContentType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(file.Content, 0, 4));
        using var stream = new MemoryStream(file.Content);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.True(document.PageCount > 1);
        Assert.Equal("BDT 38,000.00", ManagementReportExporter.PdfSafeText("৳38,000.00"));
    }

    [Fact]
    public void ExportXlsx_WritesBrandingNumericCurrencyAndWrappedLongItems()
    {
        var report = CreateReport(rowCount: 1);
        var file = CreateExporter().ExportXlsx(report, Shop());

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        using var stream = new MemoryStream(file.Content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Report");

        Assert.Equal("Green Energy", sheet.Cell(1, 1).GetString());
        Assert.Equal("Monthly sales report", sheet.Cell(3, 1).GetString());
        Assert.True(sheet.Cell(7, 4).DataType == XLDataType.Number);
        Assert.Contains("BDT", sheet.Cell(7, 4).Style.NumberFormat.Format);
        Assert.True(sheet.Cell(7, 5).Style.Alignment.WrapText);
        Assert.True(sheet.Column(5).Width >= 40d);
        Assert.Equal(38_000m, sheet.Cell(7, 4).GetValue<decimal>());
    }

    [Fact]
    public void ExportXlsx_PreservesSafeTextForFormulaLikeProductDescriptions()
    {
        var report = CreateReport(rowCount: 1, itemText: "=unsafe item description");
        var file = CreateExporter().ExportXlsx(report, Shop());

        using var stream = new MemoryStream(file.Content);
        using var workbook = new XLWorkbook(stream);
        var item = workbook.Worksheet("Report").Cell(7, 5);
        Assert.Equal("=unsafe item description", item.GetString());
        Assert.Equal(XLDataType.Text, item.DataType);
    }

    private static IManagementReportExporter CreateExporter()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddReporting();
        return services.BuildServiceProvider().GetRequiredService<IManagementReportExporter>();
    }

    private static ShopSettingsResponse Shop() => new(
        "Green Energy", "Dhaka", "01700000000", "shop@example.test", null,
        "BIN-123", "Thank you for your business.");

    private static ManagementReportPreview CreateReport(int rowCount, string? itemText = null)
    {
        var columns = new[]
        {
            new ManagementReportColumn("invoice", "Invoice"),
            new ManagementReportColumn("customer", "Customer"),
            new ManagementReportColumn("date", "Date"),
            new ManagementReportColumn("total", "Total", true),
            new ManagementReportColumn("items", "Items")
        };
        var rows = Enumerable.Range(1, rowCount).Select(index => new ManagementReportRow(
            new Dictionary<string, ManagementReportCell>
            {
                ["invoice"] = new($"INV-{index:000000}"),
                ["customer"] = new("A customer with a deliberately long display name"),
                ["date"] = new("2026-10-16"),
                ["total"] = new("৳38,000.00", 38_000m),
                ["items"] = new(itemText ?? "Solar Battery 100W with an intentionally long description that must wrap safely in both PDF and Excel exports.")
            }));
        return new ManagementReportPreview(
            ManagementReportKind.MonthlySales,
            "Monthly sales report",
            "01 Oct 2026 – 31 Oct 2026",
            columns,
            rows.ToArray(),
            [new ManagementReportSummary("Sales total", "৳38,000.00", 38_000m)]);
    }
}
