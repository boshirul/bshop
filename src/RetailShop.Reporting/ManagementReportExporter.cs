using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using RetailShop.Application.Reports;
using RetailShop.Application.Settings;

namespace RetailShop.Reporting;

public sealed record ManagementReportFile(string FileName, string ContentType, byte[] Content);

public interface IManagementReportExporter
{
    ManagementReportFile ExportPdf(ManagementReportPreview report, ShopSettingsResponse shop);
    ManagementReportFile ExportXlsx(ManagementReportPreview report, ShopSettingsResponse shop);
}

internal sealed class ManagementReportExporter(IConfiguration configuration) : IManagementReportExporter
{
    private const long MaxLogoBytes = 2 * 1024 * 1024;
    private readonly string mediaRoot = Path.GetFullPath(
        configuration["MediaStorage:RootPath"] ?? Path.Combine("..", "RetailShop.Media"),
        Directory.GetCurrentDirectory());
    public ManagementReportFile ExportPdf(ManagementReportPreview report, ShopSettingsResponse shop)
    {
        using var document = new PdfDocument();
        var landscape = report.Columns.Count > 5;
        var page = AddPage(document, landscape);
        var graphics = XGraphics.FromPdfPage(page);
        var regular = new XFont("Arial", 8);
        var bold = new XFont("Arial", 8, XFontStyleEx.Bold);
        var heading = new XFont("Arial", 16, XFontStyleEx.Bold);
        var margin = 36d;
        var y = margin;
        DrawHeader(graphics, shop, report, margin, ref y, heading, regular);
        var widths = ColumnWidths(page.Width.Point - (margin * 2), report.Columns.Count);
        DrawTableHeader(graphics, report, widths, margin, ref y, bold);
        foreach (var row in report.Rows)
        {
            const double rowHeight = 20;
            if (y + rowHeight > page.Height.Point - margin - 70)
            {
                page = AddPage(document, landscape);
                graphics.Dispose();
                graphics = XGraphics.FromPdfPage(page);
                y = margin;
                DrawHeader(graphics, shop, report, margin, ref y, heading, regular);
                DrawTableHeader(graphics, report, widths, margin, ref y, bold);
            }
            var x = margin;
            foreach (var (column, width) in report.Columns.Zip(widths))
            {
                row.Values.TryGetValue(column.Key, out var cell);
                var format = column.IsNumeric ? XStringFormats.CenterRight : XStringFormats.CenterLeft;
                graphics.DrawString(cell?.Display ?? string.Empty, regular, XBrushes.Black,
                    new XRect(x + 3, y + 3, width - 6, rowHeight - 6), format);
                graphics.DrawLine(XPens.LightGray, x, y + rowHeight, x + width, y + rowHeight);
                x += width;
            }
            y += rowHeight;
        }
        y += 12;
        foreach (var summary in report.Summary)
        {
            graphics.DrawString(summary.Label, bold, XBrushes.Black, new XRect(margin, y, 180, 14), XStringFormats.TopLeft);
            graphics.DrawString(summary.Value, bold, XBrushes.Black, new XRect(margin + 180, y, 120, 14), XStringFormats.TopRight);
            y += 16;
        }
        graphics.DrawString(shop.ReceiptFooter, regular, XBrushes.DimGray,
            new XRect(margin, page.Height.Point - margin, page.Width.Point - (margin * 2), 12), XStringFormats.TopCenter);
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return new ManagementReportFile(FileName(report, "pdf"), "application/pdf", stream.ToArray());
    }

    public ManagementReportFile ExportXlsx(ManagementReportPreview report, ShopSettingsResponse shop)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        sheet.Cell(1, 1).Value = shop.ShopName;
        sheet.Cell(2, 1).Value = report.Title;
        sheet.Cell(3, 1).Value = report.PeriodLabel;
        var headerRow = 5;
        for (var column = 0; column < report.Columns.Count; column++)
        {
            var cell = sheet.Cell(headerRow, column + 1);
            cell.Value = report.Columns.ElementAt(column).Heading;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("1F4E78");
            cell.Style.Font.FontColor = XLColor.White;
        }
        var rowIndex = headerRow + 1;
        foreach (var row in report.Rows)
        {
            for (var column = 0; column < report.Columns.Count; column++)
            {
                var definition = report.Columns.ElementAt(column);
                row.Values.TryGetValue(definition.Key, out var value);
                var cell = sheet.Cell(rowIndex, column + 1);
                if (definition.IsNumeric && value?.Numeric is decimal number)
                {
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = definition.Heading.Contains("margin", StringComparison.OrdinalIgnoreCase)
                        ? "0.00%" : "#,##0.00";
                }
                else cell.Value = SafeText(value?.Display);
            }
            rowIndex++;
        }
        if (report.Summary.Count > 0)
        {
            rowIndex++;
            foreach (var summary in report.Summary)
            {
                sheet.Cell(rowIndex, 1).Value = summary.Label;
                if (summary.Numeric is decimal numeric)
                {
                    sheet.Cell(rowIndex, 2).Value = numeric;
                    sheet.Cell(rowIndex, 2).Style.NumberFormat.Format = "#,##0.00";
                }
                else sheet.Cell(rowIndex, 2).Value = SafeText(summary.Value);
                sheet.Cell(rowIndex, 1).Style.Font.Bold = true;
                rowIndex++;
            }
        }
        var range = sheet.Range(headerRow, 1, Math.Max(headerRow, rowIndex - 1), report.Columns.Count);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        sheet.Columns().AdjustToContents(8, 42);
        sheet.SheetView.FreezeRows(headerRow);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ManagementReportFile(FileName(report, "xlsx"), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", stream.ToArray());
    }

    private static PdfPage AddPage(PdfDocument document, bool landscape)
    {
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        if (landscape) page.Orientation = PdfSharp.PageOrientation.Landscape;
        return page;
    }

    private void DrawHeader(XGraphics g, ShopSettingsResponse shop, ManagementReportPreview report, double margin, ref double y, XFont heading, XFont regular)
    {
        var logoWidth = TryDrawLogo(g, shop.LogoUrl, margin, y) ? 58d : 0d;
        g.DrawString(shop.ShopName, heading, XBrushes.Black, new XRect(margin + logoWidth, y, 400 - logoWidth, 22), XStringFormats.TopLeft);
        y += 25;
        var contact = string.Join(" · ", new[] { shop.Address, shop.Phone, shop.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
        g.DrawString(contact, regular, XBrushes.DimGray, new XRect(margin, y, 500, 14), XStringFormats.TopLeft);
        y += 22;
        g.DrawString(report.Title, new XFont("Arial", 11, XFontStyleEx.Bold), XBrushes.Black, new XRect(margin, y, 400, 16), XStringFormats.TopLeft);
        g.DrawString(report.PeriodLabel, regular, XBrushes.DimGray, new XRect(margin, y, 500, 16), XStringFormats.TopRight);
        y += 25;
    }

    private bool TryDrawLogo(XGraphics graphics, string? logoKey, double x, double y)
    {
        var path = ResolveLogoPath(logoKey);
        if (path is null) return false;
        try
        {
            using var image = XImage.FromFile(path);
            var ratio = image.PixelWidth == 0 ? 1 : (double)image.PixelHeight / image.PixelWidth;
            var width = 48d;
            var height = Math.Min(34d, width * ratio);
            graphics.DrawImage(image, x, y, width, height);
            return true;
        }
        catch
        {
            // Invalid or unsupported assets never prevent an authorized report export.
            return false;
        }
    }

    private string? ResolveLogoPath(string? logoKey)
    {
        if (string.IsNullOrWhiteSpace(logoKey) || Path.IsPathRooted(logoKey) ||
            Uri.TryCreate(logoKey, UriKind.Absolute, out _)) return null;
        var extension = Path.GetExtension(logoKey);
        if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) return null;
        var root = Path.TrimEndingDirectorySeparator(mediaRoot) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(logoKey.Replace('/', Path.DirectorySeparatorChar), mediaRoot);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return null;
        var info = new FileInfo(path);
        if (info.Length == 0 || info.Length > MaxLogoBytes) return null;
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> signature = stackalloc byte[8];
        var read = input.Read(signature);
        var png = read >= 8 && signature[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = read >= 3 && signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF;
        return png || jpeg ? path : null;
    }

    private static void DrawTableHeader(XGraphics g, ManagementReportPreview report, double[] widths, double margin, ref double y, XFont bold)
    {
        var x = margin;
        foreach (var (column, width) in report.Columns.Zip(widths))
        {
            g.DrawRectangle(XBrushes.LightGray, x, y, width, 18);
            g.DrawString(column.Heading, bold, XBrushes.Black, new XRect(x + 3, y + 3, width - 6, 12), column.IsNumeric ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
            x += width;
        }
        y += 18;
    }

    private static double[] ColumnWidths(double available, int count) => Enumerable.Repeat(available / Math.Max(count, 1), Math.Max(count, 1)).ToArray();
    private static string SafeText(string? value) => !string.IsNullOrEmpty(value) && "=+-@".Contains(value[0]) ? "'" + value : value ?? string.Empty;
    private static string FileName(ManagementReportPreview report, string extension) => $"bshop-{report.Kind.ToString().ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMdd}.{extension}";
}
