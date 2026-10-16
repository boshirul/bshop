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
    private const double Margin = 36d;
    private const double FooterHeight = 24d;
    private const string CurrencyFormat = "\"BDT\" #,##0.00";
    private readonly string mediaRoot = Path.GetFullPath(
        configuration["MediaStorage:RootPath"] ?? Path.Combine("..", "RetailShop.Media"),
        Directory.GetCurrentDirectory());

    public ManagementReportFile ExportPdf(ManagementReportPreview report, ShopSettingsResponse shop)
    {
        using var document = new PdfDocument();
        var landscape = report.Columns.Count > 5;
        var regular = new XFont("Arial", 8);
        var bold = new XFont("Arial", 8, XFontStyleEx.Bold);
        var heading = new XFont("Arial", 16, XFontStyleEx.Bold);
        var tableHeading = new XFont("Arial", 7.5, XFontStyleEx.Bold);
        var page = AddPage(document, landscape);
        var graphics = XGraphics.FromPdfPage(page);
        var y = Margin;
        var widths = ColumnWidths(page.Width.Point - (Margin * 2), report.Columns);
        DrawHeader(graphics, shop, report, ref y, heading, regular);
        DrawTableHeader(graphics, report, widths, ref y, tableHeading);

        foreach (var row in report.Rows)
        {
            var cells = report.Columns.Select(column => PdfSafeText(
                row.Values.TryGetValue(column.Key, out var cell) ? cell.Display : string.Empty)).ToArray();
            var lines = cells.Select((text, index) => WrapText(text, regular, widths[index] - 6)).ToArray();
            var rowHeight = Math.Max(18d, lines.Max(line => line.Count) * 9.5d + 6d);
            if (y + rowHeight > ContentBottom(page))
            {
                graphics.Dispose();
                page = AddPage(document, landscape);
                graphics = XGraphics.FromPdfPage(page);
                y = Margin;
                DrawHeader(graphics, shop, report, ref y, heading, regular);
                DrawTableHeader(graphics, report, widths, ref y, tableHeading);
            }

            DrawRow(graphics, report.Columns, widths, lines, y, rowHeight, regular);
            y += rowHeight;
        }

        y += 10;
        foreach (var summary in report.Summary)
        {
            const double summaryHeight = 16d;
            if (y + summaryHeight > ContentBottom(page))
            {
                graphics.Dispose();
                page = AddPage(document, landscape);
                graphics = XGraphics.FromPdfPage(page);
                y = Margin;
                DrawHeader(graphics, shop, report, ref y, heading, regular);
            }
            graphics.DrawString(PdfSafeText(summary.Label), bold, XBrushes.Black,
                new XRect(Margin, y, 220, summaryHeight), XStringFormats.TopLeft);
            graphics.DrawString(PdfSafeText(summary.Value), bold, XBrushes.Black,
                new XRect(Margin + 220, y, 130, summaryHeight), XStringFormats.TopRight);
            y += summaryHeight;
        }
        graphics.Dispose();

        DrawFooters(document, shop, regular);
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return new ManagementReportFile(FileName(report, "pdf"), "application/pdf", stream.ToArray());
    }

    public ManagementReportFile ExportXlsx(ManagementReportPreview report, ShopSettingsResponse shop)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        var columnCount = Math.Max(1, report.Columns.Count);
        var contact = string.Join(" · ", new[] { shop.Address, shop.Phone, shop.Email }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        sheet.Cell(1, 1).Value = shop.ShopName;
        sheet.Range(1, 1, 1, columnCount).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("17365D");
        if (!string.IsNullOrWhiteSpace(contact))
        {
            sheet.Cell(2, 1).Value = contact;
            sheet.Range(2, 1, 2, columnCount).Merge();
            sheet.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("5B6573");
        }
        sheet.Cell(3, 1).Value = report.Title;
        sheet.Range(3, 1, 3, columnCount).Merge();
        sheet.Cell(3, 1).Style.Font.Bold = true;
        sheet.Cell(4, 1).Value = report.PeriodLabel;
        sheet.Range(4, 1, 4, columnCount).Merge();
        sheet.Cell(4, 1).Style.Font.Italic = true;
        sheet.Cell(4, 1).Style.Font.FontColor = XLColor.FromHtml("5B6573");

        const int headerRow = 6;
        for (var column = 0; column < report.Columns.Count; column++)
        {
            var definition = report.Columns.ElementAt(column);
            var cell = sheet.Cell(headerRow, column + 1);
            cell.Value = definition.Heading;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("17365D");
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Horizontal = definition.IsNumeric
                ? XLAlignmentHorizontalValues.Right
                : XLAlignmentHorizontalValues.Left;
            ConfigureColumn(sheet.Column(column + 1), definition);
        }

        var rowIndex = headerRow + 1;
        foreach (var row in report.Rows)
        {
            for (var column = 0; column < report.Columns.Count; column++)
            {
                var definition = report.Columns.ElementAt(column);
                row.Values.TryGetValue(definition.Key, out var value);
                var cell = sheet.Cell(rowIndex, column + 1);
                WriteCell(cell, definition, value);
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                cell.Style.Alignment.WrapText = IsLongTextColumn(definition);
                cell.Style.Alignment.Horizontal = definition.IsNumeric
                    ? XLAlignmentHorizontalValues.Right
                    : XLAlignmentHorizontalValues.Left;
            }
            sheet.Row(rowIndex).AdjustToContents(15d, 72d);
            rowIndex++;
        }

        if (report.Summary.Count > 0)
        {
            rowIndex++;
            foreach (var summary in report.Summary)
            {
                var label = sheet.Cell(rowIndex, 1);
                label.Value = summary.Label;
                label.Style.Font.Bold = true;
                label.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                if (summary.Numeric is decimal numeric)
                {
                    var value = sheet.Cell(rowIndex, Math.Min(2, columnCount));
                    value.Value = numeric;
                    value.Style.NumberFormat.Format = CurrencyFormat;
                    value.Style.Font.Bold = true;
                    value.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    value.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                }
                else sheet.Cell(rowIndex, Math.Min(2, columnCount)).Value = SafeText(summary.Value);
                rowIndex++;
            }
        }

        if (report.Columns.Count > 0)
        {
            var range = sheet.Range(headerRow, 1, Math.Max(headerRow, rowIndex - 1), report.Columns.Count);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        }
        sheet.SheetView.FreezeRows(headerRow);
        sheet.SheetView.FreezeColumns(1);
        sheet.PageSetup.PageOrientation = report.Columns.Count > 5
            ? XLPageOrientation.Landscape
            : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ManagementReportFile(FileName(report, "xlsx"),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", stream.ToArray());
    }

    internal static string PdfSafeText(string? value) =>
        (value ?? string.Empty).Replace("৳", "BDT ", StringComparison.Ordinal);

    private static PdfPage AddPage(PdfDocument document, bool landscape)
    {
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        if (landscape) page.Orientation = PdfSharp.PageOrientation.Landscape;
        return page;
    }

    private void DrawHeader(XGraphics graphics, ShopSettingsResponse shop,
        ManagementReportPreview report, ref double y, XFont heading, XFont regular)
    {
        var logoWidth = TryDrawLogo(graphics, shop.LogoUrl, Margin, y) ? 58d : 0d;
        var detailsX = Margin + logoWidth;
        graphics.DrawString(PdfSafeText(shop.ShopName), heading, XBrushes.Black,
            new XRect(detailsX, y, 370 - logoWidth, 22), XStringFormats.TopLeft);
        y += 23;

        var contactParts = new[] { shop.Address, shop.Phone, shop.Email,
                string.IsNullOrWhiteSpace(shop.TaxRegistrationNumber) ? null : $"Tax ID: {shop.TaxRegistrationNumber}" }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        foreach (var line in WrapText(PdfSafeText(string.Join(" · ", contactParts)), regular, 500))
        {
            graphics.DrawString(line, regular, XBrushes.DimGray,
                new XRect(Margin, y, 500, 11), XStringFormats.TopLeft);
            y += 10;
        }
        y += 5;
        graphics.DrawString(PdfSafeText(report.Title), new XFont("Arial", 11, XFontStyleEx.Bold), XBrushes.Black,
            new XRect(Margin, y, 390, 16), XStringFormats.TopLeft);
        graphics.DrawString(PdfSafeText(report.PeriodLabel), regular, XBrushes.DimGray,
            new XRect(Margin + 390, y, 140, 16), XStringFormats.TopRight);
        y += 22;
        graphics.DrawLine(XPens.LightGray, Margin, y, graphics.PageSize.Width - Margin, y);
        y += 8;
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
            var height = Math.Min(36d, width * ratio);
            graphics.DrawImage(image, x, y, width, height);
            return true;
        }
        catch
        {
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

    private static void DrawTableHeader(XGraphics graphics, ManagementReportPreview report,
        IReadOnlyList<double> widths, ref double y, XFont bold)
    {
        const double height = 24d;
        var x = Margin;
        foreach (var (column, width) in report.Columns.Zip(widths))
        {
            graphics.DrawRectangle(XBrushes.LightGray, x, y, width, height);
            var lines = WrapText(PdfSafeText(column.Heading), bold, width - 6).Take(2).ToArray();
            for (var index = 0; index < lines.Length; index++)
            {
                graphics.DrawString(lines[index], bold, XBrushes.Black,
                    new XRect(x + 3, y + 3 + (index * 8.5d), width - 6, 10),
                    column.IsNumeric ? XStringFormats.TopRight : XStringFormats.TopLeft);
            }
            x += width;
        }
        y += height;
    }

    private static void DrawRow(XGraphics graphics, IReadOnlyCollection<ManagementReportColumn> columns,
        IReadOnlyList<double> widths, IReadOnlyList<IReadOnlyList<string>> lines, double y,
        double rowHeight, XFont regular)
    {
        var x = Margin;
        foreach (var ((column, width), cellLines) in columns.Zip(widths).Zip(lines))
        {
            for (var index = 0; index < cellLines.Count; index++)
            {
                graphics.DrawString(cellLines[index], regular, XBrushes.Black,
                    new XRect(x + 3, y + 3 + (index * 9.5d), width - 6, 10),
                    column.IsNumeric ? XStringFormats.TopRight : XStringFormats.TopLeft);
            }
            graphics.DrawLine(XPens.LightGray, x, y + rowHeight, x + width, y + rowHeight);
            x += width;
        }
    }

    private static void DrawFooters(PdfDocument document, ShopSettingsResponse shop, XFont regular)
    {
        for (var index = 0; index < document.PageCount; index++)
        {
            var page = document.Pages[index];
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawLine(XPens.LightGray, Margin, page.Height.Point - Margin - FooterHeight + 4,
                page.Width.Point - Margin, page.Height.Point - Margin - FooterHeight + 4);
            graphics.DrawString(PdfSafeText(shop.ReceiptFooter), regular, XBrushes.DimGray,
                new XRect(Margin, page.Height.Point - Margin - 12, page.Width.Point - (Margin * 2) - 60, 12), XStringFormats.TopLeft);
            graphics.DrawString($"Page {index + 1} of {document.PageCount}", regular, XBrushes.DimGray,
                new XRect(page.Width.Point - Margin - 60, page.Height.Point - Margin - 12, 60, 12), XStringFormats.TopRight);
        }
    }

    private static double ContentBottom(PdfPage page) => page.Height.Point - Margin - FooterHeight;

    private static IReadOnlyList<double> ColumnWidths(double available, IReadOnlyCollection<ManagementReportColumn> columns)
    {
        var weights = columns.Select(column => column.Key switch
        {
            "items" or "description" => 3.6d,
            "customer" or "product" or "payee" => 1.8d,
            "invoice" or "voucher" or "code" or "date" or "status" or "payment" => 1.1d,
            _ when column.IsNumeric => 1.0d,
            _ => 1.3d
        }).ToArray();
        var totalWeight = weights.Sum();
        return weights.Select(weight => available * (weight / totalWeight)).ToArray();
    }

    private static IReadOnlyList<string> WrapText(string value, XFont font, double width)
    {
        if (string.IsNullOrWhiteSpace(value)) return [string.Empty];
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        var line = string.Empty;
        foreach (var word in words)
        {
            var proposed = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
            if (Measure(proposed, font) <= width || string.IsNullOrEmpty(line)) { line = proposed; continue; }
            result.Add(line);
            line = word;
        }
        if (!string.IsNullOrEmpty(line)) result.Add(line);
        return result;
    }

    private static double Measure(string value, XFont font) => value.Length * (font.Size * 0.56d);

    private static void ConfigureColumn(IXLColumn column, ManagementReportColumn definition)
    {
        column.Width = definition.Key switch
        {
            "items" or "description" => 44d,
            "product" or "customer" or "payee" => 28d,
            "invoice" or "voucher" => 17d,
            "date" => 13d,
            _ when definition.IsNumeric => 15d,
            _ => 18d
        };
    }

    private static void WriteCell(IXLCell cell, ManagementReportColumn definition, ManagementReportCell? value)
    {
        if (definition.IsNumeric && value?.Numeric is decimal number)
        {
            cell.Value = number;
            cell.Style.NumberFormat.Format = IsPercentageColumn(definition) ? "0.00%" : IsCurrencyColumn(definition) ? CurrencyFormat : "#,##0.00";
            return;
        }
        if (definition.Key == "date" && DateOnly.TryParse(value?.Display, out var date))
        {
            cell.Value = date.ToDateTime(TimeOnly.MinValue);
            cell.Style.NumberFormat.Format = "dd mmm yyyy";
            return;
        }
        cell.Value = SafeText(value?.Display);
    }

    private static bool IsLongTextColumn(ManagementReportColumn column) => column.Key is "items" or "description" or "product" or "customer" or "payee";
    private static bool IsPercentageColumn(ManagementReportColumn column) => column.Key.Contains("margin", StringComparison.OrdinalIgnoreCase) || column.Heading.Contains("margin", StringComparison.OrdinalIgnoreCase);
    private static bool IsCurrencyColumn(ManagementReportColumn column) =>
        column.Key is "total" or "discount" or "tax" or "paid" or "due" or "revenue" or "cost" or "profit" or "averageCost" or "stockValue" or "creditLimit" or "balance" or "amount" ||
        column.Heading.Contains("amount", StringComparison.OrdinalIgnoreCase) || column.Heading.Contains("total", StringComparison.OrdinalIgnoreCase) || column.Heading.Contains("cost", StringComparison.OrdinalIgnoreCase) || column.Heading.Contains("value", StringComparison.OrdinalIgnoreCase) || column.Heading.Contains("balance", StringComparison.OrdinalIgnoreCase);
    private static string SafeText(string? value) => !string.IsNullOrEmpty(value) && "=+-@".Contains(value[0]) ? "'" + value : value ?? string.Empty;
    private static string FileName(ManagementReportPreview report, string extension) => $"bshop-{report.Kind.ToString().ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMdd}.{extension}";
}
