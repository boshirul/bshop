using System.ComponentModel.DataAnnotations;
using RetailShop.Application.Common;

namespace RetailShop.Application.Products;

public sealed record NamedMasterDataItem(
    Guid Id,
    string Name,
    bool IsActive);

public sealed record SubCategoryItem(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    bool IsActive);

public sealed record ProductModelItem(
    Guid Id,
    Guid BrandId,
    string BrandName,
    string Name,
    bool IsActive);

public sealed record UnitItem(
    Guid Id,
    string Name,
    string Symbol,
    bool IsActive);

public sealed record SaveNamedMasterDataRequest(
    [param: Required, MaxLength(150)] string Name,
    bool IsActive = true);

public sealed record SaveSubCategoryRequest(
    Guid CategoryId,
    [param: Required, MaxLength(150)] string Name,
    bool IsActive = true);

public sealed record SaveProductModelRequest(
    Guid BrandId,
    [param: Required, MaxLength(150)] string Name,
    bool IsActive = true);

public sealed record SaveUnitRequest(
    [param: Required, MaxLength(100)] string Name,
    [param: Required, MaxLength(20)] string Symbol,
    bool IsActive = true);

public sealed record SaveProductRequest(
    [param: Required, MaxLength(250)] string Name,
    Guid CategoryId,
    Guid? SubCategoryId,
    Guid? BrandId,
    Guid? ProductModelId,
    Guid UnitId,
    [param: MaxLength(150)] string? VariantName,
    [param: MaxLength(2000)] string? Description,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal MinimumStockLevel,
    bool IsWarrantyAvailable,
    int? WarrantyMonths,
    bool IsSerialRequired,
    bool IsVatApplicable,
    bool AllowOnlineSale,
    bool IsActive = true);

public sealed record AddProductImageRequest(
    [param: Required, MaxLength(1000)] string Url,
    [param: MaxLength(250)] string? AltText,
    bool IsPrimary);

public sealed record ProductListItem(
    Guid Id,
    string ProductCode,
    string Barcode,
    string Name,
    string CategoryName,
    string? BrandName,
    string UnitSymbol,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal AverageCost,
    bool IsWarrantyAvailable,
    bool IsSerialRequired,
    bool AllowOnlineSale,
    bool IsActive,
    string? ImageUrl);

public sealed record ProductImageItem(
    Guid Id,
    string Url,
    string? AltText,
    bool IsPrimary);

public sealed record ProductDetail(
    Guid Id,
    string ProductCode,
    string Barcode,
    string Name,
    Guid CategoryId,
    string CategoryName,
    Guid? SubCategoryId,
    string? SubCategoryName,
    Guid? BrandId,
    string? BrandName,
    Guid? ProductModelId,
    string? ProductModelName,
    Guid UnitId,
    string UnitName,
    string UnitSymbol,
    string? VariantName,
    string? Description,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal AverageCost,
    decimal MinimumStockLevel,
    bool IsWarrantyAvailable,
    int? WarrantyMonths,
    bool IsSerialRequired,
    bool IsVatApplicable,
    bool AllowOnlineSale,
    bool IsActive,
    IReadOnlyCollection<ProductImageItem> Images);

public interface IProductMasterDataService
{
    Task<IReadOnlyCollection<NamedMasterDataItem>> GetCategoriesAsync(
        CancellationToken cancellationToken);
    Task<OperationResult<NamedMasterDataItem>> CreateCategoryAsync(
        SaveNamedMasterDataRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<NamedMasterDataItem>> UpdateCategoryAsync(
        Guid id,
        SaveNamedMasterDataRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteCategoryAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubCategoryItem>> GetSubCategoriesAsync(
        Guid? categoryId,
        CancellationToken cancellationToken);
    Task<OperationResult<SubCategoryItem>> CreateSubCategoryAsync(
        SaveSubCategoryRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<SubCategoryItem>> UpdateSubCategoryAsync(
        Guid id,
        SaveSubCategoryRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteSubCategoryAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NamedMasterDataItem>> GetBrandsAsync(
        CancellationToken cancellationToken);
    Task<OperationResult<NamedMasterDataItem>> CreateBrandAsync(
        SaveNamedMasterDataRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<NamedMasterDataItem>> UpdateBrandAsync(
        Guid id,
        SaveNamedMasterDataRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteBrandAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProductModelItem>> GetProductModelsAsync(
        Guid? brandId,
        CancellationToken cancellationToken);
    Task<OperationResult<ProductModelItem>> CreateProductModelAsync(
        SaveProductModelRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<ProductModelItem>> UpdateProductModelAsync(
        Guid id,
        SaveProductModelRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteProductModelAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UnitItem>> GetUnitsAsync(CancellationToken cancellationToken);
    Task<OperationResult<UnitItem>> CreateUnitAsync(
        SaveUnitRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<UnitItem>> UpdateUnitAsync(
        Guid id,
        SaveUnitRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteUnitAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);
}

public interface IProductService
{
    Task<PagedResult<ProductListItem>> GetProductsAsync(
        string? search,
        Guid? categoryId,
        Guid? brandId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<OperationResult<ProductDetail>> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken);
    Task<OperationResult<ProductDetail>> GetByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken);
    Task<OperationResult<ProductDetail>> CreateProductAsync(
        SaveProductRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<ProductDetail>> UpdateProductAsync(
        Guid id,
        SaveProductRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteProductAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<ProductImageItem>> AddImageAsync(
        Guid productId,
        AddProductImageRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteImageAsync(
        Guid productId,
        Guid imageId,
        Guid performedBy,
        CancellationToken cancellationToken);
}
