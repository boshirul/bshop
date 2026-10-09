using RetailShop.Domain.Common;

namespace RetailShop.Domain.DataExchange;

public sealed class ExportLog : AuditableEntity
{
    private ExportLog()
    {
    }

    public ExportLog(
        DataExchangeKind kind,
        string fileName,
        int rowCount,
        Guid performedBy)
    {
        Kind = kind;
        FileName = fileName;
        RowCount = rowCount;
        CreatedBy = performedBy;
    }

    public DataExchangeKind Kind { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public int RowCount { get; private set; }
}
