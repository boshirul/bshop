using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Returns;
using RetailShop.Application.Security;
using RetailShop.Domain.Returns;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/returns")]
[Authorize]
public sealed class ReturnsController(IReturnService service) : ControllerBase
{
    [HttpGet("complaint-reasons")]
    [Authorize(Policy = Permissions.Returns.Request)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ComplaintReasonItem>>>>
        ComplaintReasons(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<ComplaintReasonItem>>.Success(
            await service.GetComplaintReasonsAsync(cancellationToken)));

    [HttpGet("invoice/{invoiceNumber}")]
    [Authorize(Policy = Permissions.Returns.Request)]
    public async Task<ActionResult<ApiResponse<ReturnInvoiceItem>>> Invoice(
        string invoiceNumber,
        CancellationToken cancellationToken)
    {
        var result = await service.GetInvoiceAsync(invoiceNumber, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<ReturnInvoiceItem>.Success(result.Value!))
            : NotFound(ApiResponse<ReturnInvoiceItem>.Failure(result.Errors));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Returns.Request)]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesReturnListItem>>>> List(
        [FromQuery] string? search,
        [FromQuery] SalesReturnStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<SalesReturnListItem>>.Success(
            await service.GetReturnsAsync(
                search, status, page, pageSize, cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Returns.Request)]
    public async Task<ActionResult<ApiResponse<SalesReturnDetailItem>>> Detail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetReturnAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SalesReturnDetailItem>.Success(result.Value!))
            : NotFound(ApiResponse<SalesReturnDetailItem>.Failure(result.Errors));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Returns.Request)]
    public async Task<ActionResult<ApiResponse<SalesReturnDetailItem>>> Create(
        CreateSalesReturnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateReturnAsync(
            request, User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(Detail),
                new { id = result.Value!.Id },
                ApiResponse<SalesReturnDetailItem>.Success(result.Value))
            : BadRequest(ApiResponse<SalesReturnDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Permissions.Returns.Approve)]
    public async Task<ActionResult<ApiResponse<SalesReturnDetailItem>>> Approve(
        Guid id,
        ReviewSalesReturnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveAsync(
            id, request, User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SalesReturnDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<SalesReturnDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Permissions.Returns.Approve)]
    public async Task<ActionResult<ApiResponse<SalesReturnDetailItem>>> Reject(
        Guid id,
        RejectSalesReturnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectAsync(
            id, request, User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SalesReturnDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<SalesReturnDetailItem>.Failure(result.Errors));
    }
}
