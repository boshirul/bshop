using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Products;
using RetailShop.Domain.Products;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Products;

internal sealed class ProductService(
    RetailShopDbContext dbContext,
    IAuditService auditService,
    IConfiguration? configuration = null,
    IHostEnvironment? hostEnvironment = null) : IProductService
{
    private const int MaxImages = 9;
    private const int MaxVideos = 2;
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const long MaxVideoBytes = 100 * 1024 * 1024;
    // Resolve relative locations from the content root so deployments can mount media
    // separately from the API's published application directory.
    private readonly string mediaRoot = Path.GetFullPath(
        configuration?["MediaStorage:RootPath"] ?? Path.Combine("..", "RetailShop.Media"),
        hostEnvironment?.ContentRootPath ?? Directory.GetCurrentDirectory());
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
                product.Media
                    .Where(media => media.MediaType == ProductMediaType.Image && media.IsPrimary)
                    .Select(media => "/api/products/" + product.Id + "/media/" + media.Id + "/content")
                    .FirstOrDefault() ?? product.Images
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

    public async Task<IReadOnlyCollection<ProductMediaItem>> GetMediaAsync(
        Guid productId,
        CancellationToken cancellationToken) =>
        await dbContext.ProductMedia.AsNoTracking()
            .Where(item => item.ProductId == productId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.SortOrder)
            .Select(item => ToMediaItem(item))
            .ToArrayAsync(cancellationToken);

    public async Task<OperationResult<ProductMediaItem>> AddMediaAsync(
        Guid productId,
        ProductMediaUpload upload,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(
            item => item.Id == productId,
            cancellationToken);
        if (product is null)
        {
            return OperationResult<ProductMediaItem>.Failure("Product not found.");
        }

        var classification = await ValidateUploadAsync(upload, cancellationToken);
        if (classification.Error is not null)
        {
            return OperationResult<ProductMediaItem>.Failure(classification.Error);
        }

        var count = await dbContext.ProductMedia.CountAsync(
            item => item.ProductId == productId && item.MediaType == classification.Type,
            cancellationToken);
        var limit = classification.Type == ProductMediaType.Image ? MaxImages : MaxVideos;
        if (count >= limit)
        {
            return OperationResult<ProductMediaItem>.Failure(
                $"A product can have at most {limit} {classification.Type.ToString().ToLowerInvariant()} files.");
        }

        var storageKey = await StoreUploadAsync(
            product.Name,
            upload,
            classification,
            isPoster: false,
            cancellationToken);
        try
        {
            var hasPrimaryImage = await dbContext.ProductMedia.AnyAsync(
                item => item.ProductId == productId && item.MediaType == ProductMediaType.Image && item.IsPrimary,
                cancellationToken);
            var lastSortOrder = await dbContext.ProductMedia
                .Where(item => item.ProductId == productId)
                .Select(item => (int?)item.SortOrder)
                .MaxAsync(cancellationToken) ?? -1;
            var media = new ProductMedia(
                productId,
                classification.Type,
                storageKey,
                classification.Type == ProductMediaType.Image ? storageKey : null,
                classification.ContentType,
                upload.Length,
                classification.Type == ProductMediaType.Image && !hasPrimaryImage,
                lastSortOrder + 1)
            {
                CreatedBy = performedBy
            };
            dbContext.ProductMedia.Add(media);
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync("Create", "ProductMedia", media.Id.ToString(),
                $"Added {classification.Type.ToString().ToLowerInvariant()} media to product {productId}.", performedBy, cancellationToken);
            return OperationResult<ProductMediaItem>.Success(ToMediaItem(media));
        }
        catch
        {
            DeleteStoredFile(storageKey);
            throw;
        }
    }

    public async Task<OperationResult<ProductMediaItem>> ReplaceMediaAsync(
        Guid productId,
        Guid mediaId,
        ProductMediaUpload upload,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var media = await dbContext.ProductMedia.Include(item => item.Product).SingleOrDefaultAsync(
            item => item.Id == mediaId && item.ProductId == productId, cancellationToken);
        if (media is null) return OperationResult<ProductMediaItem>.Failure("Product media was not found.");

        var classification = await ValidateUploadAsync(upload, cancellationToken);
        if (classification.Error is not null) return OperationResult<ProductMediaItem>.Failure(classification.Error);
        if (classification.Type != media.MediaType)
        {
            return OperationResult<ProductMediaItem>.Failure("Replacement media must be the same type as the existing item.");
        }

        var storageKey = await StoreUploadAsync(
            media.Product.Name,
            upload,
            classification,
            isPoster: false,
            cancellationToken);
        var oldKey = media.StorageKey;
        var oldThumbnailKey = media.ThumbnailStorageKey;
        try
        {
            media.ReplaceFile(storageKey,
                classification.Type == ProductMediaType.Image ? storageKey : oldThumbnailKey,
                classification.ContentType, upload.Length);
            media.LastModifiedBy = performedBy;
            await dbContext.SaveChangesAsync(cancellationToken);
            DeleteStoredFile(oldKey);
            if (classification.Type == ProductMediaType.Image && !string.IsNullOrWhiteSpace(oldThumbnailKey) && oldThumbnailKey != oldKey)
                DeleteStoredFile(oldThumbnailKey);
            await auditService.WriteAsync("Update", "ProductMedia", media.Id.ToString(),
                "Replaced product media.", performedBy, cancellationToken);
            return OperationResult<ProductMediaItem>.Success(ToMediaItem(media));
        }
        catch
        {
            DeleteStoredFile(storageKey);
            throw;
        }
    }

    public async Task<OperationResult<bool>> DeleteMediaAsync(
        Guid productId, Guid mediaId, Guid performedBy, CancellationToken cancellationToken)
    {
        var media = await dbContext.ProductMedia.Include(item => item.Product).SingleOrDefaultAsync(
            item => item.Id == mediaId && item.ProductId == productId, cancellationToken);
        if (media is null) return OperationResult<bool>.Failure("Product media was not found.");
        var storageKey = media.StorageKey;
        var thumbnailStorageKey = media.ThumbnailStorageKey;
        media.DeletedBy = performedBy;
        dbContext.ProductMedia.Remove(media);
        await dbContext.SaveChangesAsync(cancellationToken);
        DeleteStoredFile(storageKey);
        if (!string.IsNullOrWhiteSpace(thumbnailStorageKey) && thumbnailStorageKey != storageKey)
            DeleteStoredFile(thumbnailStorageKey);
        await auditService.WriteAsync("Delete", "ProductMedia", mediaId.ToString(),
            "Removed product media.", performedBy, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<OperationResult<ProductMediaItem>> ReplaceVideoPosterAsync(
        Guid productId,
        Guid mediaId,
        ProductMediaUpload upload,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var media = await dbContext.ProductMedia.Include(item => item.Product).SingleOrDefaultAsync(
            item => item.Id == mediaId && item.ProductId == productId, cancellationToken);
        if (media is null || media.MediaType != ProductMediaType.Video)
            return OperationResult<ProductMediaItem>.Failure("A video must be selected before adding a poster image.");

        var classification = await ValidateUploadAsync(upload, cancellationToken);
        if (classification.Error is not null) return OperationResult<ProductMediaItem>.Failure(classification.Error);
        if (classification.Type != ProductMediaType.Image)
            return OperationResult<ProductMediaItem>.Failure("Video posters must be JPEG, PNG, or WebP images.");

        var storageKey = await StoreUploadAsync(
            media.Product.Name,
            upload,
            classification,
            isPoster: true,
            cancellationToken);
        var oldKey = media.ThumbnailStorageKey;
        try
        {
            media.ReplaceThumbnail(storageKey);
            media.LastModifiedBy = performedBy;
            await dbContext.SaveChangesAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(oldKey)) DeleteStoredFile(oldKey);
            await auditService.WriteAsync("Update", "ProductMedia", media.Id.ToString(),
                "Updated video poster image.", performedBy, cancellationToken);
            return OperationResult<ProductMediaItem>.Success(ToMediaItem(media));
        }
        catch
        {
            DeleteStoredFile(storageKey);
            throw;
        }
    }

    public async Task<OperationResult<bool>> SetPrimaryMediaAsync(
        Guid productId, Guid mediaId, Guid performedBy, CancellationToken cancellationToken)
    {
        var selected = await dbContext.ProductMedia.SingleOrDefaultAsync(
            item => item.Id == mediaId && item.ProductId == productId, cancellationToken);
        if (selected is null || selected.MediaType != ProductMediaType.Image)
            return OperationResult<bool>.Failure("Select an image from this product's gallery.");
        var images = await dbContext.ProductMedia.Where(item =>
            item.ProductId == productId && item.MediaType == ProductMediaType.Image).ToArrayAsync(cancellationToken);
        foreach (var image in images) { image.IsPrimary = image.Id == mediaId; image.LastModifiedBy = performedBy; }
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<OperationResult<IReadOnlyCollection<ProductMediaItem>>> ReorderMediaAsync(
        Guid productId, ReorderProductMediaRequest request, Guid performedBy, CancellationToken cancellationToken)
    {
        var media = await dbContext.ProductMedia.Where(item => item.ProductId == productId)
            .ToArrayAsync(cancellationToken);
        if (media.Length != request.MediaIds.Count || media.Any(item => !request.MediaIds.Contains(item.Id)))
            return OperationResult<IReadOnlyCollection<ProductMediaItem>>.Failure("The gallery order does not match this product's media.");
        for (var index = 0; index < request.MediaIds.Count; index++)
        {
            var item = media.Single(value => value.Id == request.MediaIds.ElementAt(index));
            item.SortOrder = index;
            item.LastModifiedBy = performedBy;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<IReadOnlyCollection<ProductMediaItem>>.Success(
            media.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.SortOrder).Select(ToMediaItem).ToArray());
    }

    public async Task<OperationResult<ProductMediaFile>> GetMediaFileAsync(
        Guid productId, Guid mediaId, bool thumbnail, bool publicOnly, CancellationToken cancellationToken)
    {
        var media = await dbContext.ProductMedia.AsNoTracking().Include(item => item.Product)
            .SingleOrDefaultAsync(item => item.Id == mediaId && item.ProductId == productId, cancellationToken);
        if (media is null || (publicOnly && (!media.Product.IsActive || !media.Product.AllowOnlineSale)))
            return OperationResult<ProductMediaFile>.Failure("Media was not found.");
        var key = thumbnail && !string.IsNullOrWhiteSpace(media.ThumbnailStorageKey)
            ? media.ThumbnailStorageKey : media.StorageKey;
        var path = ResolvePath(key);
        var contentType = thumbnail && !string.IsNullOrWhiteSpace(media.ThumbnailStorageKey)
            ? GetContentTypeFromStorageKey(key)
            : media.ContentType;
        return File.Exists(path)
            ? OperationResult<ProductMediaFile>.Success(new ProductMediaFile(path, contentType))
            : OperationResult<ProductMediaFile>.Failure("Media file was not found.");
    }

    private static ProductMediaItem ToMediaItem(ProductMedia media) => new(
        media.Id, media.MediaType.ToString(),
        $"/api/products/{media.ProductId}/media/{media.Id}/content",
        media.ThumbnailStorageKey is null ? null : $"/api/products/{media.ProductId}/media/{media.Id}/content?thumbnail=true",
        media.ContentType, media.FileSize, media.IsPrimary, media.SortOrder, media.CreatedOn);

    private async Task<(ProductMediaType Type, string ContentType, string? Error)> ValidateUploadAsync(
        ProductMediaUpload upload, CancellationToken cancellationToken)
    {
        if (upload.Length <= 0) return (default, string.Empty, "Select a non-empty media file.");
        if (!upload.Content.CanSeek) return (default, string.Empty, "The uploaded file stream is not readable.");
        var header = new byte[32];
        var read = await upload.Content.ReadAsync(header.AsMemory(), cancellationToken);
        upload.Content.Position = 0;
        var isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var isWebp = read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        var isMp4 = read >= 12 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8);
        if (isJpeg || isPng || isWebp)
        {
            if (upload.Length > MaxImageBytes) return (default, string.Empty, "Images must not exceed 5 MB.");
            return (ProductMediaType.Image, isJpeg ? "image/jpeg" : isPng ? "image/png" : "image/webp", null);
        }
        if (isMp4)
        {
            if (upload.Length > MaxVideoBytes) return (default, string.Empty, "Videos must not exceed 100 MB.");
            return (ProductMediaType.Video, "video/mp4", null);
        }
        return (default, string.Empty, "Only valid JPEG, PNG, WebP, and browser-compatible MP4 files are allowed.");
    }

    private async Task<string> StoreUploadAsync(
        string productName,
        ProductMediaUpload upload,
        (ProductMediaType Type, string ContentType, string? Error) classification,
        bool isPoster,
        CancellationToken cancellationToken)
    {
        var extension = classification.Type == ProductMediaType.Video ? ".mp4" : classification.ContentType switch
        { "image/jpeg" => ".jpg", "image/png" => ".png", _ => ".webp" };
        var category = isPoster
            ? "posters"
            : classification.Type == ProductMediaType.Video ? "videos" : "images";
        var storageKey = Path.Combine(
            "products",
            category,
            BuildStorageFileName(productName, extension));
        var destination = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".upload";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await upload.Content.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            // Windows cannot rename an open file. Dispose the upload stream before
            // atomically moving the validated file into its permanent location.
            File.Move(temporary, destination);
            return storageKey;
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    private static string BuildStorageFileName(string productName, string extension)
    {
        // Product names are trusted application data, but they are still converted
        // to an ASCII, path-safe slug before becoming part of a filesystem name.
        var normalized = productName.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        var slug = Regex.Replace(builder.ToString(), "-+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug)) slug = "product";
        slug = slug[..Math.Min(slug.Length, 72)];
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{slug}_{timestamp}_{suffix}{extension}";
    }

    private string ResolvePath(string storageKey)
    {
        var root = Path.GetFullPath(mediaRoot);
        var path = Path.GetFullPath(Path.Combine(root, storageKey));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid media storage key.");
        return path;
    }

    private void DeleteStoredFile(string storageKey)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
    }

    private static string GetContentTypeFromStorageKey(string storageKey) =>
        Path.GetExtension(storageKey).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

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
