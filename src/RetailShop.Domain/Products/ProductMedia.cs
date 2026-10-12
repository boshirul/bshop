using RetailShop.Domain.Common;

namespace RetailShop.Domain.Products;

public sealed class ProductMedia : AuditableEntity
{
    private ProductMedia() { }

    public ProductMedia(
        Guid productId,
        ProductMediaType mediaType,
        string storageKey,
        string? thumbnailStorageKey,
        string contentType,
        long fileSize,
        bool isPrimary,
        int sortOrder)
    {
        ProductId = productId;
        MediaType = mediaType;
        StorageKey = storageKey;
        ThumbnailStorageKey = thumbnailStorageKey;
        ContentType = contentType;
        FileSize = fileSize;
        IsPrimary = isPrimary;
        SortOrder = sortOrder;
    }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public ProductMediaType MediaType { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string? ThumbnailStorageKey { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }

    public void ReplaceFile(
        string storageKey,
        string? thumbnailStorageKey,
        string contentType,
        long fileSize)
    {
        StorageKey = storageKey;
        ThumbnailStorageKey = thumbnailStorageKey;
        ContentType = contentType;
        FileSize = fileSize;
    }

    public void ReplaceThumbnail(string storageKey)
    {
        ThumbnailStorageKey = storageKey;
    }
}
