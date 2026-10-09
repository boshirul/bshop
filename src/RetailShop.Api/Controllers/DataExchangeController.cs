using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.DataExchange;
using RetailShop.Application.Security;
using RetailShop.Domain.DataExchange;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/data-exchange")]
[Authorize(Policy = Permissions.DataExchange.Manage)]
public sealed class DataExchangeController(IDataExchangeService service) : ControllerBase
{
    [HttpGet("templates/{kind}")]
    public async Task<IActionResult> Template(
        DataExchangeKind kind,
        CancellationToken cancellationToken)
    {
        var file = await service.GetTemplateAsync(kind, cancellationToken);
        return File(Encoding.UTF8.GetBytes(file.Content), file.ContentType, file.FileName);
    }

    [HttpGet("exports/{kind}")]
    public async Task<IActionResult> Export(
        DataExchangeKind kind,
        CancellationToken cancellationToken)
    {
        var result = await service.ExportAsync(
            kind,
            User.GetRequiredUserId(),
            cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse<CsvFileItem>.Failure(result.Errors));
        }

        var file = result.Value!;
        return File(Encoding.UTF8.GetBytes(file.Content), file.ContentType, file.FileName);
    }

    [HttpPost("imports/preview")]
    public async Task<ActionResult<ApiResponse<ImportPreviewResult>>> Preview(
        ImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.PreviewImportAsync(request, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<ImportPreviewResult>.Success(result.Value!))
            : BadRequest(ApiResponse<ImportPreviewResult>.Failure(result.Errors));
    }

    [HttpPost("imports/commit")]
    public async Task<ActionResult<ApiResponse<ImportCommitResult>>> Commit(
        ImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CommitImportAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<ImportCommitResult>.Success(result.Value!))
            : BadRequest(ApiResponse<ImportCommitResult>.Failure(result.Errors));
    }

    [HttpGet("exports/logs")]
    public async Task<ActionResult<ApiResponse<PagedResult<ExportLogItem>>>> ExportLogs(
        [FromQuery] DataExchangeKind? kind,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<ExportLogItem>>.Success(
            await service.GetExportLogsAsync(kind, page, pageSize, cancellationToken)));
}
