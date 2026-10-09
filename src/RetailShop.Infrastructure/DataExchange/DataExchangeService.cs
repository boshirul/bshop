using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Common;
using RetailShop.Application.Contacts;
using RetailShop.Application.DataExchange;
using RetailShop.Application.Inventory;
using RetailShop.Application.Products;
using RetailShop.Domain.DataExchange;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.DataExchange;

public sealed class DataExchangeService(
    RetailShopDbContext dbContext,
    ICustomerService customerService,
    ISupplierService supplierService,
    IProductService productService,
    IInventoryService inventoryService) : IDataExchangeService
{
    private const string CsvContentType = "text/csv";
    private const int MaximumCsvCharacters = 2_000_000;
    private const int MaximumCsvRows = 5_000;

    public Task<CsvFileItem> GetTemplateAsync(
        DataExchangeKind kind,
        CancellationToken cancellationToken)
    {
        var headers = GetHeaders(kind);
        var sample = kind switch
        {
            DataExchangeKind.Customers => new[]
            {
                "Walk-in Customer",
                "01700000000",
                "customer@example.com",
                "Dhaka",
                "0",
                "Optional note",
                "true"
            },
            DataExchangeKind.Suppliers => new[]
            {
                "ABC Supplier",
                "Mr. Karim",
                "01800000000",
                "supplier@example.com",
                "Chittagong",
                "Optional note",
                "true"
            },
            DataExchangeKind.Products => new[]
            {
                "",
                "",
                "Imported IPS Battery",
                "Battery",
                "",
                "",
                "",
                "pcs",
                "",
                "Imported by CSV",
                "6500",
                "7200",
                "2",
                "true",
                "12",
                "false",
                "true",
                "true",
                "true"
            },
            DataExchangeKind.OpeningStock => new[]
            {
                "PRD-0001",
                "10",
                "6500"
            },
            _ => Array.Empty<string>()
        };

        var content = WriteCsv([headers, sample]);
        return Task.FromResult(new CsvFileItem(
            $"{ToSlug(kind)}-template.csv",
            CsvContentType,
            content));
    }

    public async Task<OperationResult<CsvFileItem>> ExportAsync(
        DataExchangeKind kind,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var headers = GetHeaders(kind);
        List<string[]> rows = [headers];

        switch (kind)
        {
            case DataExchangeKind.Customers:
                rows.AddRange(await dbContext.Customers
                    .OrderBy(customer => customer.Name)
                    .Select(customer => new[]
                    {
                        customer.Name,
                        customer.Phone,
                        customer.Email ?? string.Empty,
                        customer.Address ?? string.Empty,
                        customer.CreditLimit.ToString(CultureInfo.InvariantCulture),
                        customer.Notes ?? string.Empty,
                        customer.IsActive.ToString().ToLowerInvariant()
                    })
                    .ToListAsync(cancellationToken));
                break;
            case DataExchangeKind.Suppliers:
                rows.AddRange(await dbContext.Suppliers
                    .OrderBy(supplier => supplier.Name)
                    .Select(supplier => new[]
                    {
                        supplier.Name,
                        supplier.ContactPerson ?? string.Empty,
                        supplier.Phone,
                        supplier.Email ?? string.Empty,
                        supplier.Address ?? string.Empty,
                        supplier.Notes ?? string.Empty,
                        supplier.IsActive.ToString().ToLowerInvariant()
                    })
                    .ToListAsync(cancellationToken));
                break;
            case DataExchangeKind.Products:
                rows.AddRange(await dbContext.Products
                    .Include(product => product.Category)
                    .Include(product => product.SubCategory)
                    .Include(product => product.Brand)
                    .Include(product => product.ProductModel)
                    .Include(product => product.Unit)
                    .OrderBy(product => product.Name)
                    .Select(product => new[]
                    {
                        product.ProductCode,
                        product.Barcodes
                            .OrderBy(barcode => barcode.CreatedOn)
                            .Select(barcode => barcode.Value)
                            .FirstOrDefault() ?? string.Empty,
                        product.Name,
                        product.Category.Name,
                        product.SubCategory == null ? string.Empty : product.SubCategory.Name,
                        product.Brand == null ? string.Empty : product.Brand.Name,
                        product.ProductModel == null ? string.Empty : product.ProductModel.Name,
                        product.Unit.Symbol,
                        product.VariantName ?? string.Empty,
                        product.Description ?? string.Empty,
                        product.PurchasePrice.ToString(CultureInfo.InvariantCulture),
                        product.SalePrice.ToString(CultureInfo.InvariantCulture),
                        product.MinimumStockLevel.ToString(CultureInfo.InvariantCulture),
                        product.IsWarrantyAvailable.ToString().ToLowerInvariant(),
                        product.WarrantyMonths.HasValue
                            ? product.WarrantyMonths.Value.ToString(CultureInfo.InvariantCulture)
                            : string.Empty,
                        product.IsSerialRequired.ToString().ToLowerInvariant(),
                        product.IsVatApplicable.ToString().ToLowerInvariant(),
                        product.AllowOnlineSale.ToString().ToLowerInvariant(),
                        product.IsActive.ToString().ToLowerInvariant()
                    })
                    .ToListAsync(cancellationToken));
                break;
            case DataExchangeKind.OpeningStock:
                headers = ["productCode", "productName", "quantity", "unitCost"];
                rows = [headers];
                rows.AddRange(await dbContext.StockBalances
                    .Include(balance => balance.Product)
                    .OrderBy(balance => balance.Product.ProductCode)
                    .Select(balance => new[]
                    {
                        balance.Product.ProductCode,
                        balance.Product.Name,
                        balance.AvailableQuantity.ToString(CultureInfo.InvariantCulture),
                        balance.Product.AverageCost.ToString(CultureInfo.InvariantCulture)
                    })
                    .ToListAsync(cancellationToken));
                break;
            default:
                return OperationResult<CsvFileItem>.Failure("Unsupported export type.");
        }

        var fileName = $"{ToSlug(kind)}-export-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        dbContext.ExportLogs.Add(new ExportLog(kind, fileName, rows.Count - 1, performedBy));
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<CsvFileItem>.Success(new CsvFileItem(
            fileName,
            CsvContentType,
            WriteCsv(rows)));
    }

    public async Task<OperationResult<ImportPreviewResult>> PreviewImportAsync(
        ImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var parsed = ParseCsv(request.CsvText);
        if (!parsed.Succeeded)
        {
            return OperationResult<ImportPreviewResult>.Failure(parsed.Errors);
        }

        var rows = await ValidateRowsAsync(request.Kind, parsed.Value!, cancellationToken);
        return OperationResult<ImportPreviewResult>.Success(ToPreview(request.Kind, rows));
    }

    public async Task<OperationResult<ImportCommitResult>> CommitImportAsync(
        ImportPreviewRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var parsed = ParseCsv(request.CsvText);
        if (!parsed.Succeeded)
        {
            return OperationResult<ImportCommitResult>.Failure(parsed.Errors);
        }

        var rows = await ValidateRowsAsync(request.Kind, parsed.Value!, cancellationToken);
        var validRows = rows.Where(row => row.IsValid).ToArray();
        var invalidRows = rows.Where(row => !row.IsValid).ToArray();
        var imported = 0;
        var rowErrors = new List<ImportPreviewRow>(invalidRows);

        foreach (var row in validRows)
        {
            var result = await ImportRowAsync(request.Kind, row.Values, performedBy, cancellationToken);
            if (result.Succeeded)
            {
                imported++;
                continue;
            }

            rowErrors.Add(row with
            {
                IsValid = false,
                Errors = result.Errors
            });
        }

        var status = rowErrors.Count == 0
            ? ImportBatchStatus.Completed
            : imported == 0
                ? ImportBatchStatus.Rejected
                : ImportBatchStatus.CompletedWithErrors;
        var batch = new ImportBatch(
            GenerateBatchNumber(request.Kind),
            request.Kind,
            status,
            request.FileName,
            rows.Count,
            imported,
            rowErrors.Count,
            performedBy);

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        var errorFile = rowErrors.Count == 0
            ? null
            : BuildErrorFile(request.Kind, rowErrors);

        return OperationResult<ImportCommitResult>.Success(new ImportCommitResult(
            batch.Id,
            batch.BatchNumber,
            request.Kind,
            rows.Count,
            imported,
            rowErrors.Count,
            errorFile));
    }

    public async Task<PagedResult<ExportLogItem>> GetExportLogsAsync(
        DataExchangeKind? kind,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.ExportLogs.AsNoTracking();
        if (kind.HasValue)
        {
            query = query.Where(log => log.Kind == kind.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.CreatedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new ExportLogItem(
                log.Id,
                log.Kind,
                log.FileName,
                log.RowCount,
                log.CreatedOn,
                log.CreatedBy))
            .ToListAsync(cancellationToken);

        return new PagedResult<ExportLogItem>(items, page, pageSize, total);
    }

    private async Task<OperationResult<bool>> ImportRowAsync(
        DataExchangeKind kind,
        IReadOnlyDictionary<string, string> values,
        Guid performedBy,
        CancellationToken cancellationToken) =>
        kind switch
        {
            DataExchangeKind.Customers => await ImportCustomerAsync(values, performedBy, cancellationToken),
            DataExchangeKind.Suppliers => await ImportSupplierAsync(values, performedBy, cancellationToken),
            DataExchangeKind.Products => await ImportProductAsync(values, performedBy, cancellationToken),
            DataExchangeKind.OpeningStock => await ImportOpeningStockAsync(values, performedBy, cancellationToken),
            _ => OperationResult<bool>.Failure("Unsupported import type.")
        };

    private async Task<OperationResult<bool>> ImportCustomerAsync(
        IReadOnlyDictionary<string, string> values,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var result = await customerService.CreateAsync(
            new SaveCustomerRequest(
                Require(values, "name"),
                Require(values, "phone"),
                Optional(values, "email"),
                Optional(values, "address"),
                Optional(values, "notes"),
                Decimal(values, "creditLimit"),
                Bool(values, "isActive", true)),
            performedBy,
            cancellationToken);

        return result.Succeeded
            ? OperationResult<bool>.Success(true)
            : OperationResult<bool>.Failure(result.Errors);
    }

    private async Task<OperationResult<bool>> ImportSupplierAsync(
        IReadOnlyDictionary<string, string> values,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.CreateAsync(
            new SaveSupplierRequest(
                Require(values, "name"),
                Optional(values, "contactPerson"),
                Require(values, "phone"),
                Optional(values, "email"),
                Optional(values, "address"),
                Optional(values, "notes"),
                Bool(values, "isActive", true)),
            performedBy,
            cancellationToken);

        return result.Succeeded
            ? OperationResult<bool>.Success(true)
            : OperationResult<bool>.Failure(result.Errors);
    }

    private async Task<OperationResult<bool>> ImportProductAsync(
        IReadOnlyDictionary<string, string> values,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .FirstOrDefaultAsync(item => item.Name == Require(values, "category"), cancellationToken);
        var unit = await dbContext.Units
            .FirstOrDefaultAsync(item =>
                item.Name == Require(values, "unit") || item.Symbol == Require(values, "unit"),
                cancellationToken);
        var subCategoryName = Optional(values, "subCategory");
        var brandName = Optional(values, "brand");
        var modelName = Optional(values, "model");
        var subCategory = subCategoryName is null
            ? null
            : await dbContext.SubCategories.FirstOrDefaultAsync(
                item => item.Name == subCategoryName && item.CategoryId == category!.Id,
                cancellationToken);
        var brand = brandName is null
            ? null
            : await dbContext.Brands.FirstOrDefaultAsync(item => item.Name == brandName, cancellationToken);
        var model = modelName is null || brand is null
            ? null
            : await dbContext.ProductModels.FirstOrDefaultAsync(
                item => item.Name == modelName && item.BrandId == brand.Id,
                cancellationToken);

        var result = await productService.CreateProductAsync(
            new SaveProductRequest(
                Require(values, "name"),
                category!.Id,
                subCategory?.Id,
                brand?.Id,
                model?.Id,
                unit!.Id,
                Optional(values, "variantName"),
                Optional(values, "description"),
                Decimal(values, "purchasePrice"),
                Decimal(values, "salePrice"),
                Decimal(values, "minimumStockLevel"),
                Bool(values, "isWarrantyAvailable", false),
                IntOptional(values, "warrantyMonths"),
                Bool(values, "isSerialRequired", false),
                Bool(values, "isVatApplicable", false),
                Bool(values, "allowOnlineSale", false),
                Bool(values, "isActive", true)),
            performedBy,
            cancellationToken);

        return result.Succeeded
            ? OperationResult<bool>.Success(true)
            : OperationResult<bool>.Failure(result.Errors);
    }

    private async Task<OperationResult<bool>> ImportOpeningStockAsync(
        IReadOnlyDictionary<string, string> values,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var code = Require(values, "productCode");
        var product = await dbContext.Products.FirstOrDefaultAsync(
            item => item.ProductCode == code,
            cancellationToken);
        if (product is null)
        {
            return OperationResult<bool>.Failure($"Product code '{code}' was not found.");
        }

        var result = await inventoryService.RecordOpeningStockAsync(
            new OpeningStockRequest(
                [
                    new OpeningStockItemRequest(
                        product.Id,
                        Decimal(values, "quantity"),
                        Decimal(values, "unitCost"))
                ],
                "Imported opening stock from CSV."),
            performedBy,
            cancellationToken);

        return result.Succeeded
            ? OperationResult<bool>.Success(true)
            : OperationResult<bool>.Failure(result.Errors);
    }

    private async Task<IReadOnlyCollection<ImportPreviewRow>> ValidateRowsAsync(
        DataExchangeKind kind,
        ParsedCsv parsed,
        CancellationToken cancellationToken)
    {
        var headers = GetHeaders(kind);
        var missingHeaders = headers
            .Where(header => !parsed.Headers.Contains(header, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (missingHeaders.Length > 0)
        {
            return
            [
                new ImportPreviewRow(
                    1,
                    false,
                    [$"Missing required columns: {string.Join(", ", missingHeaders)}"],
                    new Dictionary<string, string>())
            ];
        }

        var duplicatePhones = parsed.Rows
            .Select(row => Optional(row.Values, "phone"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<ImportPreviewRow>();
        foreach (var row in parsed.Rows)
        {
            var errors = new List<string>();
            ValidateRequired(row.Values, errors, "name");

            switch (kind)
            {
                case DataExchangeKind.Customers:
                    ValidateRequired(row.Values, errors, "phone");
                    ValidateDecimal(row.Values, errors, "creditLimit", 0);
                    ValidateBool(row.Values, errors, "isActive");
                    await ValidatePhoneAsync(dbContext.Customers.Select(item => item.Phone), row.Values, duplicatePhones, errors, cancellationToken);
                    break;
                case DataExchangeKind.Suppliers:
                    ValidateRequired(row.Values, errors, "phone");
                    ValidateBool(row.Values, errors, "isActive");
                    await ValidatePhoneAsync(dbContext.Suppliers.Select(item => item.Phone), row.Values, duplicatePhones, errors, cancellationToken);
                    break;
                case DataExchangeKind.Products:
                    await ValidateProductAsync(row.Values, errors, cancellationToken);
                    break;
                case DataExchangeKind.OpeningStock:
                    errors.Clear();
                    ValidateRequired(row.Values, errors, "productCode");
                    ValidateDecimal(row.Values, errors, "quantity", 0.001m);
                    ValidateDecimal(row.Values, errors, "unitCost", 0);
                    await ValidateOpeningStockAsync(row.Values, errors, cancellationToken);
                    break;
            }

            results.Add(new ImportPreviewRow(
                row.RowNumber,
                errors.Count == 0,
                errors,
                row.Values));
        }

        return results;
    }

    private async Task ValidateProductAsync(
        IReadOnlyDictionary<string, string> values,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Optional(values, "productCode")) ||
            !string.IsNullOrWhiteSpace(Optional(values, "barcode")))
        {
            errors.Add("Product code and barcode are system-generated; leave both columns blank for imports.");
        }

        ValidateRequired(values, errors, "name");
        ValidateRequired(values, errors, "category");
        ValidateRequired(values, errors, "unit");
        ValidateDecimal(values, errors, "purchasePrice", 0);
        ValidateDecimal(values, errors, "salePrice", 0);
        ValidateDecimal(values, errors, "minimumStockLevel", 0);
        ValidateBool(values, errors, "isWarrantyAvailable");
        ValidateBool(values, errors, "isSerialRequired");
        ValidateBool(values, errors, "isVatApplicable");
        ValidateBool(values, errors, "allowOnlineSale");
        ValidateBool(values, errors, "isActive");

        var categoryName = Optional(values, "category");
        var unitName = Optional(values, "unit");
        var category = categoryName is null
            ? null
            : await dbContext.Categories.FirstOrDefaultAsync(item => item.Name == categoryName, cancellationToken);
        if (categoryName is not null && category is null)
        {
            errors.Add($"Category '{categoryName}' was not found.");
        }

        if (unitName is not null && !await dbContext.Units.AnyAsync(
            item => item.Name == unitName || item.Symbol == unitName,
            cancellationToken))
        {
            errors.Add($"Unit '{unitName}' was not found.");
        }

        var subCategoryName = Optional(values, "subCategory");
        if (subCategoryName is not null && category is not null && !await dbContext.SubCategories.AnyAsync(
            item => item.Name == subCategoryName && item.CategoryId == category.Id,
            cancellationToken))
        {
            errors.Add($"Sub-category '{subCategoryName}' was not found under '{categoryName}'.");
        }

        var brandName = Optional(values, "brand");
        var brand = brandName is null
            ? null
            : await dbContext.Brands.FirstOrDefaultAsync(item => item.Name == brandName, cancellationToken);
        if (brandName is not null && brand is null)
        {
            errors.Add($"Brand '{brandName}' was not found.");
        }

        var modelName = Optional(values, "model");
        if (modelName is not null && brand is null)
        {
            errors.Add("Product model requires a valid brand.");
        }
        else if (modelName is not null && brand is not null && !await dbContext.ProductModels.AnyAsync(
            item => item.Name == modelName && item.BrandId == brand.Id,
            cancellationToken))
        {
            errors.Add($"Model '{modelName}' was not found under '{brandName}'.");
        }

        var hasWarranty = Bool(values, "isWarrantyAvailable", false);
        if (hasWarranty && IntOptional(values, "warrantyMonths") is null)
        {
            errors.Add("Warranty months is required when warranty is enabled.");
        }
    }

    private async Task ValidateOpeningStockAsync(
        IReadOnlyDictionary<string, string> values,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var code = Optional(values, "productCode");
        if (code is null)
        {
            return;
        }

        var product = await dbContext.Products.FirstOrDefaultAsync(
            item => item.ProductCode == code,
            cancellationToken);
        if (product is null)
        {
            errors.Add($"Product code '{code}' was not found.");
            return;
        }

        var alreadyHasStock = await dbContext.StockTransactions.AnyAsync(
            item => item.ProductId == product.Id,
            cancellationToken);
        if (alreadyHasStock)
        {
            errors.Add($"Product '{code}' already has stock transactions; opening stock cannot be imported for it.");
        }
    }

    private static async Task ValidatePhoneAsync(
        IQueryable<string> existingPhones,
        IReadOnlyDictionary<string, string> values,
        ISet<string> duplicatePhones,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var phone = Optional(values, "phone");
        if (phone is null)
        {
            return;
        }

        if (duplicatePhones.Contains(phone))
        {
            errors.Add($"Phone '{phone}' is duplicated in this file.");
        }

        if (await existingPhones.AnyAsync(item => item == phone, cancellationToken))
        {
            errors.Add($"Phone '{phone}' already exists.");
        }
    }

    private static ImportPreviewResult ToPreview(
        DataExchangeKind kind,
        IReadOnlyCollection<ImportPreviewRow> rows) =>
        new(
            kind,
            rows.Count,
            rows.Count(row => row.IsValid),
            rows.Count(row => !row.IsValid),
            rows);

    private static CsvFileItem BuildErrorFile(
        DataExchangeKind kind,
        IReadOnlyCollection<ImportPreviewRow> rows)
    {
        var headers = GetHeaders(kind).Concat(["errors"]).ToArray();
        var data = new List<string[]> { headers };
        data.AddRange(rows.Select(row =>
            GetHeaders(kind)
                .Select(header => row.Values.TryGetValue(header, out var value) ? value : string.Empty)
                .Concat([string.Join("; ", row.Errors)])
                .ToArray()));

        return new CsvFileItem(
            $"{ToSlug(kind)}-import-errors-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv",
            CsvContentType,
            WriteCsv(data));
    }

    private static string[] GetHeaders(DataExchangeKind kind) =>
        kind switch
        {
            DataExchangeKind.Customers =>
            [
                "name", "phone", "email", "address", "creditLimit", "notes", "isActive"
            ],
            DataExchangeKind.Suppliers =>
            [
                "name", "contactPerson", "phone", "email", "address", "notes", "isActive"
            ],
            DataExchangeKind.Products =>
            [
                "productCode", "barcode", "name", "category", "subCategory", "brand",
                "model", "unit", "variantName", "description", "purchasePrice",
                "salePrice", "minimumStockLevel", "isWarrantyAvailable",
                "warrantyMonths", "isSerialRequired", "isVatApplicable",
                "allowOnlineSale", "isActive"
            ],
            DataExchangeKind.OpeningStock =>
            [
                "productCode", "quantity", "unitCost"
            ],
            _ => []
        };

    private static OperationResult<ParsedCsv> ParseCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return OperationResult<ParsedCsv>.Failure("CSV content is empty.");
        }
        if (csv.Length > MaximumCsvCharacters)
        {
            return OperationResult<ParsedCsv>.Failure(
                "CSV content is too large. Maximum allowed size is 2 MB.");
        }

        var records = new List<string[]>();
        var field = new StringBuilder();
        var current = new List<string>();
        var inQuotes = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var ch = csv[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                current.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\r')
            {
            }
            else if (ch == '\n')
            {
                current.Add(field.ToString());
                field.Clear();
                if (current.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    records.Add(current.ToArray());
                }
                current = [];
            }
            else
            {
                field.Append(ch);
            }
        }

        current.Add(field.ToString());
        if (current.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            records.Add(current.ToArray());
        }

        if (records.Count < 2)
        {
            return OperationResult<ParsedCsv>.Failure("CSV must contain a header row and at least one data row.");
        }
        if (records.Count - 1 > MaximumCsvRows)
        {
            return OperationResult<ParsedCsv>.Failure(
                $"CSV contains too many data rows. Maximum allowed rows: {MaximumCsvRows}.");
        }

        var headers = records[0].Select(header => header.Trim()).ToArray();
        var rows = records
            .Skip(1)
            .Select((record, index) =>
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Length; i++)
                {
                    values[headers[i]] = i < record.Length ? record[i].Trim() : string.Empty;
                }

                return new ParsedCsvRow(index + 2, values);
            })
            .ToArray();

        return OperationResult<ParsedCsv>.Success(new ParsedCsv(headers, rows));
    }

    private static string WriteCsv(IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static string GenerateBatchNumber(DataExchangeKind kind) =>
        $"IMP-{ToSlug(kind).ToUpperInvariant()}-{DateTimeOffset.UtcNow:yyMMddHHmmss}";

    private static string ToSlug(DataExchangeKind kind) =>
        kind.ToString().Replace("Stock", "-stock").ToLowerInvariant();

    private static string Require(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static string? Optional(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static decimal Decimal(IReadOnlyDictionary<string, string> values, string key) =>
        decimal.TryParse(Require(values, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static int? IntOptional(IReadOnlyDictionary<string, string> values, string key) =>
        int.TryParse(Require(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static bool Bool(IReadOnlyDictionary<string, string> values, string key, bool defaultValue) =>
        bool.TryParse(Require(values, key), out var value) ? value : defaultValue;

    private static void ValidateRequired(
        IReadOnlyDictionary<string, string> values,
        ICollection<string> errors,
        string key)
    {
        if (string.IsNullOrWhiteSpace(Require(values, key)))
        {
            errors.Add($"{key} is required.");
        }
    }

    private static void ValidateDecimal(
        IReadOnlyDictionary<string, string> values,
        ICollection<string> errors,
        string key,
        decimal minimum)
    {
        if (!decimal.TryParse(Require(values, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ||
            value < minimum)
        {
            errors.Add($"{key} must be a number greater than or equal to {minimum.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void ValidateBool(
        IReadOnlyDictionary<string, string> values,
        ICollection<string> errors,
        string key)
    {
        var value = Require(values, key);
        if (!string.IsNullOrWhiteSpace(value) && !bool.TryParse(value, out _))
        {
            errors.Add($"{key} must be true or false.");
        }
    }

    private sealed record ParsedCsv(
        IReadOnlyCollection<string> Headers,
        IReadOnlyCollection<ParsedCsvRow> Rows);

    private sealed record ParsedCsvRow(
        int RowNumber,
        IReadOnlyDictionary<string, string> Values);
}
