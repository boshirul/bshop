using System.ComponentModel.DataAnnotations;
using RetailShop.Application.Common;
using RetailShop.Domain.DataExchange;

namespace RetailShop.Application.DataExchange;

public sealed record CsvFileItem(
    string FileName,
    string ContentType,
    string Content);

public sealed record ImportPreviewRequest(
    DataExchangeKind Kind,
    [param: Required, MaxLength(2_000_000)] string CsvText,
    [param: MaxLength(260)] string? FileName);

public sealed record ImportPreviewRow(
    int RowNumber,
    bool IsValid,
    IReadOnlyCollection<string> Errors,
    IReadOnlyDictionary<string, string> Values);

public sealed record ImportPreviewResult(
    DataExchangeKind Kind,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    IReadOnlyCollection<ImportPreviewRow> Rows);

public sealed record ImportCommitResult(
    Guid BatchId,
    string BatchNumber,
    DataExchangeKind Kind,
    int TotalRows,
    int ImportedRows,
    int InvalidRows,
    CsvFileItem? ErrorFile);

public sealed record ExportLogItem(
    Guid Id,
    DataExchangeKind Kind,
    string FileName,
    int RowCount,
    DateTimeOffset ExportedOn,
    Guid? ExportedBy);

public interface IDataExchangeService
{
    Task<CsvFileItem> GetTemplateAsync(
        DataExchangeKind kind,
        CancellationToken cancellationToken);

    Task<OperationResult<CsvFileItem>> ExportAsync(
        DataExchangeKind kind,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<ImportPreviewResult>> PreviewImportAsync(
        ImportPreviewRequest request,
        CancellationToken cancellationToken);

    Task<OperationResult<ImportCommitResult>> CommitImportAsync(
        ImportPreviewRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<PagedResult<ExportLogItem>> GetExportLogsAsync(
        DataExchangeKind? kind,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
