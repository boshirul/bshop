using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Products;
using RetailShop.Domain.Products;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Products;

internal sealed class ProductMasterDataService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : IProductMasterDataService
{
    public async Task<IReadOnlyCollection<NamedMasterDataItem>> GetCategoriesAsync(
        CancellationToken cancellationToken) =>
        await dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new NamedMasterDataItem(
                category.Id,
                category.Name,
                category.IsActive))
            .ToArrayAsync(cancellationToken);

    public async Task<OperationResult<NamedMasterDataItem>> CreateCategoryAsync(
        SaveNamedMasterDataRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var duplicate = await dbContext.Categories.AnyAsync(
            category => category.NormalizedName == Normalize(request.Name),
            cancellationToken);
        if (duplicate)
        {
            return OperationResult<NamedMasterDataItem>.Failure(
                "A category with this name already exists.");
        }

        var category = new Category(request.Name)
        {
            IsActive = request.IsActive,
            CreatedBy = performedBy
        };
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Create", "Category", category.Id, category.Name, performedBy, cancellationToken);
        return OperationResult<NamedMasterDataItem>.Success(Map(category));
    }

    public async Task<OperationResult<NamedMasterDataItem>> UpdateCategoryAsync(
        Guid id,
        SaveNamedMasterDataRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FindAsync([id], cancellationToken);
        if (category is null)
        {
            return OperationResult<NamedMasterDataItem>.Failure("Category not found.");
        }

        var duplicate = await dbContext.Categories.AnyAsync(
            item => item.Id != id && item.NormalizedName == Normalize(request.Name),
            cancellationToken);
        if (duplicate)
        {
            return OperationResult<NamedMasterDataItem>.Failure(
                "A category with this name already exists.");
        }

        category.Rename(request.Name);
        category.IsActive = request.IsActive;
        category.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Update", "Category", category.Id, category.Name, performedBy, cancellationToken);
        return OperationResult<NamedMasterDataItem>.Success(Map(category));
    }

    public async Task<OperationResult<bool>> DeleteCategoryAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FindAsync([id], cancellationToken);
        if (category is null)
        {
            return OperationResult<bool>.Failure("Category not found.");
        }

        if (await dbContext.Products.AnyAsync(product => product.CategoryId == id, cancellationToken) ||
            await dbContext.SubCategories.AnyAsync(item => item.CategoryId == id, cancellationToken))
        {
            return OperationResult<bool>.Failure(
                "The category is in use and cannot be deleted.");
        }

        category.DeletedBy = performedBy;
        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Delete", "Category", category.Id, category.Name, performedBy, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<IReadOnlyCollection<SubCategoryItem>> GetSubCategoriesAsync(
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SubCategories.AsNoTracking();
        if (categoryId.HasValue)
        {
            query = query.Where(item => item.CategoryId == categoryId);
        }

        return await query
            .OrderBy(item => item.Category.Name)
            .ThenBy(item => item.Name)
            .Select(item => new SubCategoryItem(
                item.Id,
                item.CategoryId,
                item.Category.Name,
                item.Name,
                item.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OperationResult<SubCategoryItem>> CreateSubCategoryAsync(
        SaveSubCategoryRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FindAsync(
            [request.CategoryId],
            cancellationToken);
        if (category is null)
        {
            return OperationResult<SubCategoryItem>.Failure("Category not found.");
        }

        var duplicate = await dbContext.SubCategories.AnyAsync(
            item =>
                item.CategoryId == request.CategoryId &&
                item.NormalizedName == Normalize(request.Name),
            cancellationToken);
        if (duplicate)
        {
            return OperationResult<SubCategoryItem>.Failure(
                "This subcategory already exists in the selected category.");
        }

        var item = new SubCategory(request.CategoryId, request.Name)
        {
            IsActive = request.IsActive,
            CreatedBy = performedBy
        };
        dbContext.SubCategories.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Create", "SubCategory", item.Id, item.Name, performedBy, cancellationToken);
        return OperationResult<SubCategoryItem>.Success(
            new SubCategoryItem(item.Id, item.CategoryId, category.Name, item.Name, item.IsActive));
    }

    public async Task<OperationResult<SubCategoryItem>> UpdateSubCategoryAsync(
        Guid id,
        SaveSubCategoryRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.SubCategories.FindAsync([id], cancellationToken);
        var category = await dbContext.Categories.FindAsync(
            [request.CategoryId],
            cancellationToken);
        if (item is null || category is null)
        {
            return OperationResult<SubCategoryItem>.Failure(
                item is null ? "Subcategory not found." : "Category not found.");
        }

        var duplicate = await dbContext.SubCategories.AnyAsync(
            candidate =>
                candidate.Id != id &&
                candidate.CategoryId == request.CategoryId &&
                candidate.NormalizedName == Normalize(request.Name),
            cancellationToken);
        if (duplicate)
        {
            return OperationResult<SubCategoryItem>.Failure(
                "This subcategory already exists in the selected category.");
        }

        item.Update(request.CategoryId, request.Name);
        item.IsActive = request.IsActive;
        item.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Update", "SubCategory", item.Id, item.Name, performedBy, cancellationToken);
        return OperationResult<SubCategoryItem>.Success(
            new SubCategoryItem(item.Id, item.CategoryId, category.Name, item.Name, item.IsActive));
    }

    public async Task<OperationResult<bool>> DeleteSubCategoryAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.SubCategories.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return OperationResult<bool>.Failure("Subcategory not found.");
        }

        if (await dbContext.Products.AnyAsync(
                product => product.SubCategoryId == id,
                cancellationToken))
        {
            return OperationResult<bool>.Failure(
                "The subcategory is in use and cannot be deleted.");
        }

        item.DeletedBy = performedBy;
        dbContext.SubCategories.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Delete", "SubCategory", item.Id, item.Name, performedBy, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<IReadOnlyCollection<NamedMasterDataItem>> GetBrandsAsync(
        CancellationToken cancellationToken) =>
        await dbContext.Brands
            .AsNoTracking()
            .OrderBy(brand => brand.Name)
            .Select(brand => new NamedMasterDataItem(brand.Id, brand.Name, brand.IsActive))
            .ToArrayAsync(cancellationToken);

    public async Task<OperationResult<NamedMasterDataItem>> CreateBrandAsync(
        SaveNamedMasterDataRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Brands.AnyAsync(
                brand => brand.NormalizedName == Normalize(request.Name),
                cancellationToken))
        {
            return OperationResult<NamedMasterDataItem>.Failure(
                "A brand with this name already exists.");
        }

        var brand = new Brand(request.Name)
        {
            IsActive = request.IsActive,
            CreatedBy = performedBy
        };
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Create", "Brand", brand.Id, brand.Name, performedBy, cancellationToken);
        return OperationResult<NamedMasterDataItem>.Success(Map(brand));
    }

    public async Task<OperationResult<NamedMasterDataItem>> UpdateBrandAsync(
        Guid id,
        SaveNamedMasterDataRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.FindAsync([id], cancellationToken);
        if (brand is null)
        {
            return OperationResult<NamedMasterDataItem>.Failure("Brand not found.");
        }

        if (await dbContext.Brands.AnyAsync(
                item => item.Id != id && item.NormalizedName == Normalize(request.Name),
                cancellationToken))
        {
            return OperationResult<NamedMasterDataItem>.Failure(
                "A brand with this name already exists.");
        }

        brand.Rename(request.Name);
        brand.IsActive = request.IsActive;
        brand.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Update", "Brand", brand.Id, brand.Name, performedBy, cancellationToken);
        return OperationResult<NamedMasterDataItem>.Success(Map(brand));
    }

    public async Task<OperationResult<bool>> DeleteBrandAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.FindAsync([id], cancellationToken);
        if (brand is null)
        {
            return OperationResult<bool>.Failure("Brand not found.");
        }

        if (await dbContext.Products.AnyAsync(product => product.BrandId == id, cancellationToken) ||
            await dbContext.ProductModels.AnyAsync(model => model.BrandId == id, cancellationToken))
        {
            return OperationResult<bool>.Failure("The brand is in use and cannot be deleted.");
        }

        brand.DeletedBy = performedBy;
        dbContext.Brands.Remove(brand);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Delete", "Brand", brand.Id, brand.Name, performedBy, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<IReadOnlyCollection<ProductModelItem>> GetProductModelsAsync(
        Guid? brandId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProductModels.AsNoTracking();
        if (brandId.HasValue)
        {
            query = query.Where(model => model.BrandId == brandId);
        }

        return await query
            .OrderBy(model => model.Brand.Name)
            .ThenBy(model => model.Name)
            .Select(model => new ProductModelItem(
                model.Id,
                model.BrandId,
                model.Brand.Name,
                model.Name,
                model.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OperationResult<ProductModelItem>> CreateProductModelAsync(
        SaveProductModelRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.FindAsync([request.BrandId], cancellationToken);
        if (brand is null)
        {
            return OperationResult<ProductModelItem>.Failure("Brand not found.");
        }

        if (await dbContext.ProductModels.AnyAsync(
                model =>
                    model.BrandId == request.BrandId &&
                    model.NormalizedName == Normalize(request.Name),
                cancellationToken))
        {
            return OperationResult<ProductModelItem>.Failure(
                "This model already exists for the selected brand.");
        }

        var model = new ProductModel(request.BrandId, request.Name)
        {
            IsActive = request.IsActive,
            CreatedBy = performedBy
        };
        dbContext.ProductModels.Add(model);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Create", "ProductModel", model.Id, model.Name, performedBy, cancellationToken);
        return OperationResult<ProductModelItem>.Success(
            new ProductModelItem(model.Id, model.BrandId, brand.Name, model.Name, model.IsActive));
    }

    public async Task<OperationResult<ProductModelItem>> UpdateProductModelAsync(
        Guid id,
        SaveProductModelRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var model = await dbContext.ProductModels.FindAsync([id], cancellationToken);
        var brand = await dbContext.Brands.FindAsync([request.BrandId], cancellationToken);
        if (model is null || brand is null)
        {
            return OperationResult<ProductModelItem>.Failure(
                model is null ? "Product model not found." : "Brand not found.");
        }

        if (await dbContext.ProductModels.AnyAsync(
                item =>
                    item.Id != id &&
                    item.BrandId == request.BrandId &&
                    item.NormalizedName == Normalize(request.Name),
                cancellationToken))
        {
            return OperationResult<ProductModelItem>.Failure(
                "This model already exists for the selected brand.");
        }

        model.Update(request.BrandId, request.Name);
        model.IsActive = request.IsActive;
        model.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Update", "ProductModel", model.Id, model.Name, performedBy, cancellationToken);
        return OperationResult<ProductModelItem>.Success(
            new ProductModelItem(model.Id, model.BrandId, brand.Name, model.Name, model.IsActive));
    }

    public async Task<OperationResult<bool>> DeleteProductModelAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var model = await dbContext.ProductModels.FindAsync([id], cancellationToken);
        if (model is null)
        {
            return OperationResult<bool>.Failure("Product model not found.");
        }

        if (await dbContext.Products.AnyAsync(
                product => product.ProductModelId == id,
                cancellationToken))
        {
            return OperationResult<bool>.Failure(
                "The product model is in use and cannot be deleted.");
        }

        model.DeletedBy = performedBy;
        dbContext.ProductModels.Remove(model);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Delete", "ProductModel", model.Id, model.Name, performedBy, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<IReadOnlyCollection<UnitItem>> GetUnitsAsync(
        CancellationToken cancellationToken) =>
        await dbContext.Units
            .AsNoTracking()
            .OrderBy(unit => unit.Name)
            .Select(unit => new UnitItem(unit.Id, unit.Name, unit.Symbol, unit.IsActive))
            .ToArrayAsync(cancellationToken);

    public async Task<OperationResult<UnitItem>> CreateUnitAsync(
        SaveUnitRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Units.AnyAsync(
                unit => unit.NormalizedName == Normalize(request.Name),
                cancellationToken))
        {
            return OperationResult<UnitItem>.Failure(
                "A unit with this name already exists.");
        }

        var unit = new Unit(request.Name, request.Symbol)
        {
            IsActive = request.IsActive,
            CreatedBy = performedBy
        };
        dbContext.Units.Add(unit);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Create", "Unit", unit.Id, unit.Name, performedBy, cancellationToken);
        return OperationResult<UnitItem>.Success(Map(unit));
    }

    public async Task<OperationResult<UnitItem>> UpdateUnitAsync(
        Guid id,
        SaveUnitRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var unit = await dbContext.Units.FindAsync([id], cancellationToken);
        if (unit is null)
        {
            return OperationResult<UnitItem>.Failure("Unit not found.");
        }

        if (await dbContext.Units.AnyAsync(
                item => item.Id != id && item.NormalizedName == Normalize(request.Name),
                cancellationToken))
        {
            return OperationResult<UnitItem>.Failure(
                "A unit with this name already exists.");
        }

        unit.Update(request.Name, request.Symbol);
        unit.IsActive = request.IsActive;
        unit.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Update", "Unit", unit.Id, unit.Name, performedBy, cancellationToken);
        return OperationResult<UnitItem>.Success(Map(unit));
    }

    public async Task<OperationResult<bool>> DeleteUnitAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var unit = await dbContext.Units.FindAsync([id], cancellationToken);
        if (unit is null)
        {
            return OperationResult<bool>.Failure("Unit not found.");
        }

        if (await dbContext.Products.AnyAsync(product => product.UnitId == id, cancellationToken))
        {
            return OperationResult<bool>.Failure("The unit is in use and cannot be deleted.");
        }

        unit.DeletedBy = performedBy;
        dbContext.Units.Remove(unit);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Delete", "Unit", unit.Id, unit.Name, performedBy, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static NamedMasterDataItem Map(Category item) =>
        new(item.Id, item.Name, item.IsActive);

    private static NamedMasterDataItem Map(Brand item) =>
        new(item.Id, item.Name, item.IsActive);

    private static UnitItem Map(Unit item) =>
        new(item.Id, item.Name, item.Symbol, item.IsActive);

    private Task WriteAuditAsync(
        string action,
        string type,
        Guid id,
        string name,
        Guid performedBy,
        CancellationToken cancellationToken) =>
        auditService.WriteAsync(
            action,
            type,
            id.ToString(),
            $"{action}d {type.ToLowerInvariant()} '{name}'.",
            performedBy,
            cancellationToken);
}
