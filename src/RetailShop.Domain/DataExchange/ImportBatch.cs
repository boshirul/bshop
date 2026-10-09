using RetailShop.Domain.Common;

namespace RetailShop.Domain.DataExchange;

public sealed class ImportBatch : AuditableEntity
{
    private ImportBatch()
    {
    }

    public ImportBatch(
        string batchNumber,
        DataExchangeKind kind,
        ImportBatchStatus status,
        string? originalFileName,
        int totalRows,
        int importedRows,
        int invalidRows,
        Guid performedBy)
    {
        BatchNumber = batchNumber;
        Kind = kind;
        Status = status;
        OriginalFileName = originalFileName;
        TotalRows = totalRows;
        ImportedRows = importedRows;
        InvalidRows = invalidRows;
        CreatedBy = performedBy;
    }

    public string BatchNumber { get; private set; } = string.Empty;

    public DataExchangeKind Kind { get; private set; }

    public ImportBatchStatus Status { get; private set; }

    public string? OriginalFileName { get; private set; }

    public int TotalRows { get; private set; }

    public int ImportedRows { get; private set; }

    public int InvalidRows { get; private set; }
}
