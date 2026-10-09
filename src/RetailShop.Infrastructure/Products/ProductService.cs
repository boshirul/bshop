using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Products;
using RetailShop.Domain.Products;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Products;

internal sealed class ProductService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : IProductService
{
    public async Task<PagedResult<ProductListItem>> GetProductsAsync(
        string? search,
        Guid? categoryId,
        Guid? brandId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(product =>
                EF.Functions.ILike(product.Name, $"%{term}%") ||
                EF.Functions.ILike(product.ProductCode, $"%{term}%") ||
                product.Barcodes.Any(barcode =>
                    EF.Functions.ILike(barcode.Value, $"%{term}%")));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(product => product.CategoryId == categoryId);
        }

        if (brandId.HasValue)
        {
            query = query.Where(product => product.BrandId == brandId);
        }

        if (isActive.HasValue)
        {
            query = query.Where(product => product.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(product => product.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductListItem(
                product.Id,
                product.ProductCode,
                product.Barcodes
                    .Where(barcode => barcode.IsPrimary)
                    .Select(barcode => barcode.Value)
                    .FirstOrDefault() ?? string.Empty,
                product.Name,
                product.Category.Name,
                product.Brand != null ? product.Brand.Name : null,
                product.Unit.Symbol,
                product.PurchasePrice,
                product.SalePrice,
                product.AverageCost,
                product.IsWarrantyAvailable,
                product.IsSerialRequired,
                product.AllowOnlineSale,
                product.IsActive,
                product.Images
                    .Where(image => image.IsPrimary)
                    .Select(image => image.Url)
                    .FirstOrDefault()))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ProductListItem>(items, page, pageSize, totalCount);
    }

    public async Task<OperationResult<ProductDetail>> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await ProjectDetails(
                dbContext.Products.AsNoTracking().Where(product => product.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return detail is null
            ? OperationResult<ProductDetail>.Failure("Product not found.")
            : OperationResult<ProductDetail>.Success(detail);
    }

    public async Task<OperationResult<ProductDetail>> GetByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken)
    {
        var detail = await ProjectDetails(
                dbContext.Products
                    .AsNoTracking()
                    .Where(product =>
                        product.Barcodes.Any(item => item.Value == barcode)))
            .SingleOrDefaultAsync(cancellationToken);
        return detail is null
            ? OperationResult<ProductDetail>.Failure("Product not found.")
            : OperationResult<ProductDetail>.Success(detail);
    }

    public async Task<OperationResult<ProductDetail>> CreateProductAsync(
        SaveProductRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateRequestAsync(request, cancellationToken);
        if (validation.Count > 0)
        {
            return OperationResult<ProductDetail>.Failure(validation);
        }

        var product = new Product(
            await GenerateProductCodeAsync(cancellationToken),
            request.Name,
            request.CategoryId,
            request.UnitId)
        {
            CreatedBy = performedBy
        };
        Apply(product, request);
        dbContext.Products.Add(product);

        var barcode = new ProductBarcode(
            product.Id,
            await GenerateBarcodeAsync(cancellationToken),
            true)
        {
            CreatedBy = performedBy
        };
        dbContext.ProductBarcodes.Add(barcode);

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Create",
            "Product",
            product.Id.ToString(),
            $"Created product '{product.Name}' ({product.ProductCode}).",
            performedBy,
            cancellationToken);

        return await GetProductAsync(product.Id, cancellationToken);
    }

    public async Task<OperationResult<ProductDetail>> UpdateProductAsync(
        Guid id,
        SaveProductRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return OperationResult<ProductDetail>.Failure("Product not found.");
        }

        var validation = await ValidateRequestAsync(request, cancellationToken);
        if (validation.Count > 0)
        {
            return OperationResult<ProductDetail>.Failure(validation);
        }

        Apply(product, request);
        product.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Update",
            "Product",
            product.Id.ToString(),
            $"Updated product '{product.Name}' ({product.ProductCode}).",
            performedBy,
            cancellationToken);

        return await GetProductAsync(product.Id, cancellationToken);
    }

    public async Task<OperationResult<bool>> DeleteProductAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(item => item.Images)
            .Include(item => item.Barcodes)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return OperationResult<bool>.Failure("Product not found.");
        }

        if (await dbContext.StockTransactions.AnyAsync(
                transaction => transaction.ProductId == id,
                cancellationToken))
        {
            return OperationResult<bool>.Failure(
                "A product with stock history cannot be deleted. Mark it inactive instead.");
        }

        product.DeletedBy = performedBy;
        foreach (var image in product.Images)
        {
            image.DeletedBy = performedBy;
        }

        foreach (var barcode in product.Barcodes)
        {
            barcode.DeletedBy = performedBy;
        }

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Delete",
            "Product",
            product.Id.ToString(),
            $"Deleted product '{product.Name}' ({product.ProductCode}).",
            performedBy,
            cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<OperationResult<ProductImageItem>> AddImageAsync(
        Guid productId,
        AddProductImageRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (!IsValidImageUrl(request.Url))
        {
            return OperationResult<ProductImageItem>.Failure(
                "Image URL must be an HTTP(S) URL or an application-relative path.");
        }

        if (!await dbContext.Products.AnyAsync(
                product => product.Id == productId,
                cancellationToken))
        {
            return OperationResult<ProductImageItem>.Failure("Product not found.");
        }

        if (request.IsPrimary)
        {
            var existingImages = await dbContext.ProductImages
                .Where(image => image.ProductId == productId && image.IsPrimary)
                .ToArrayAsync(cancellationToken);
            foreach (var existing in existingImages)
            {
                existing.IsPrimary = false;
                existing.LastModifiedBy = performedBy;
            }
        }

        var image = new ProductImage(
            productId,
            request.Url,
            request.AltText,
            request.IsPrimary)
        {
            CreatedBy = performedBy
        };
        dbContext.ProductImages.Add(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ProductImageItem>.Success(
            new ProductImageItem(image.Id, image.Url, image.AltText, image.IsPrimary));
    }

    public async Task<OperationResult<bool>> DeleteImageAsync(
        Guid productId,
        Guid imageId,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var image = await dbContext.ProductImages.SingleOrDefaultAsync(
            item => item.Id == imageId && item.ProductId == productId,
            cancellationToken);
        if (image is null)
        {
            return OperationResult<bool>.Failure("Product image not found.");
        }

        image.DeletedBy = performedBy;
        dbContext.ProductImages.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    private static IQueryable<ProductDetail> ProjectDetails(IQueryable<Product> query) =>
        query.Select(product => new ProductDetail(
                product.Id,
                product.ProductCode,
                product.Barcodes
                    .Where(barcode => barcode.IsPrimary)
                    .Select(barcode => barcode.Value)
                    .FirstOrDefault() ?? string.Empty,
                product.Name,
                product.CategoryId,
                product.Category.Name,
                product.SubCategoryId,
                product.SubCategory != null ? product.SubCategory.Name : null,
                product.BrandId,
                product.Brand != null ? product.Brand.Name : null,
                product.ProductModelId,
                product.ProductModel != null ? product.ProductModel.Name : null,
                product.UnitId,
                product.Unit.Name,
                product.Unit.Symbol,
                product.VariantName,
                product.Description,
                product.PurchasePrice,
                product.SalePrice,
                product.AverageCost,
                product.MinimumStockLevel,
                product.IsWarrantyAvailable,
                product.WarrantyMonths,
                product.IsSerialRequired,
                product.IsVatApplicable,
                product.AllowOnlineSale,
                product.IsActive,
                product.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .Select(image => new ProductImageItem(
                        image.Id,
                        image.Url,
                        image.AltText,
                        image.IsPrimary))
                    .ToArray()));

    private async Task<IReadOnlyCollection<string>> ValidateRequestAsync(
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (request.PurchasePrice < 0 || request.SalePrice < 0 ||
            request.MinimumStockLevel < 0)
        {
            errors.Add("Prices and minimum stock cannot be negative.");
        }

        if (request.IsWarrantyAvailable &&
            (!request.WarrantyMonths.HasValue || request.WarrantyMonths <= 0))
        {
            errors.Add("Warranty months must be greater than zero.");
        }

        if (!await dbContext.Categories.AnyAsync(
                item => item.Id == request.CategoryId && item.IsActive,
                cancellationToken))
        {
            errors.Add("Select an active category.");
        }

        if (!await dbContext.Units.AnyAsync(
                item => item.Id == request.UnitId && item.IsActive,
                cancellationToken))
        {
            errors.Add("Select an active unit.");
        }

        if (request.SubCategoryId.HasValue &&
            !await dbContext.SubCategories.AnyAsync(
                item =>
                    item.Id == request.SubCategoryId &&
                    item.CategoryId == request.CategoryId &&
                    item.IsActive,
                cancellationToken))
        {
            errors.Add("The subcategory does not belong to the selected category.");
        }

        if (request.BrandId.HasValue &&
            !await dbContext.Brands.AnyAsync(
                item => item.Id == request.BrandId && item.IsActive,
                cancellationToken))
        {
            errors.Add("Select an active brand.");
        }

        if (request.ProductModelId.HasValue &&
            (!request.BrandId.HasValue ||
             !await dbContext.ProductModels.AnyAsync(
                 item =>
                     item.Id == request.ProductModelId &&
                     item.BrandId == request.BrandId &&
                     item.IsActive,
                 cancellationToken)))
        {
            errors.Add("The model does not belong to the selected brand.");
        }

        return errors;
    }

    private static void Apply(Product product, SaveProductRequest request)
    {
        product.Name = request.Name.Trim();
        product.CategoryId = request.CategoryId;
        product.SubCategoryId = request.SubCategoryId;
        product.BrandId = request.BrandId;
        product.ProductModelId = request.ProductModelId;
        product.UnitId = request.UnitId;
        product.VariantName = request.VariantName?.Trim();
        product.Description = request.Description?.Trim();
        product.PurchasePrice = request.PurchasePrice;
        product.SalePrice = request.SalePrice;
        product.MinimumStockLevel = request.MinimumStockLevel;
        product.IsWarrantyAvailable = request.IsWarrantyAvailable;
        product.WarrantyMonths =
            request.IsWarrantyAvailable ? request.WarrantyMonths : null;
        product.IsSerialRequired = request.IsSerialRequired;
        product.IsVatApplicable = request.IsVatApplicable;
        product.AllowOnlineSale = request.AllowOnlineSale;
        product.IsActive = request.IsActive;
    }

    private async Task<string> GenerateProductCodeAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"PRD-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            if (!await dbContext.Products.IgnoreQueryFilters().AnyAsync(
                    product => product.ProductCode == code,
                    cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique product code.");
    }

    private async Task<string> GenerateBarcodeAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bytes = RandomNumberGenerator.GetBytes(11);
            var digits = "2" + string.Concat(bytes.Select(value => value % 10));
            var barcode = digits + CalculateEan13CheckDigit(digits);

            if (!await dbContext.ProductBarcodes.IgnoreQueryFilters().AnyAsync(
                    item => item.Value == barcode,
                    cancellationToken))
            {
                return barcode;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique barcode.");
    }

    private static int CalculateEan13CheckDigit(string twelveDigits)
    {
        var sum = twelveDigits
            .Select((character, index) =>
                (character - '0') * (index % 2 == 0 ? 1 : 3))
            .Sum();
        return (10 - sum % 10) % 10;
    }

    private static bool IsValidImageUrl(string value) =>
        value.StartsWith("/", StringComparison.Ordinal) ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
