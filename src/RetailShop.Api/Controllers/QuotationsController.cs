using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Quotations;
using RetailShop.Application.Sales;
using RetailShop.Application.Security;
using RetailShop.Domain.Quotations;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/quotations")]
[Authorize(Policy = Permissions.Quotations.View)]
public sealed class QuotationsController(IQuotationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<QuotationListItem>>>> Get(
        [FromQuery] string? search,
        [FromQuery] QuotationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<QuotationListItem>>.Success(
            await service.GetAsync(
                search,
                status,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<QuotationDetailItem>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<QuotationDetailItem>.Success(result.Value!))
            : NotFound(ApiResponse<QuotationDetailItem>.Failure(result.Errors));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Quotations.Manage)]
    public async Task<ActionResult<ApiResponse<QuotationDetailItem>>> Create(
        SaveQuotationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            request,
            User.GetRequiredUserId(),
            User.HasPermission(Permissions.Sales.ChangePrice),
            User.HasPermission(Permissions.Sales.Discount),
            cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(GetById),
                new { id = result.Value!.Id },
                ApiResponse<QuotationDetailItem>.Success(result.Value))
            : BadRequest(ApiResponse<QuotationDetailItem>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Quotations.Manage)]
    public async Task<ActionResult<ApiResponse<QuotationDetailItem>>> Update(
        Guid id,
        SaveQuotationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(
            id,
            request,
            User.GetRequiredUserId(),
            User.HasPermission(Permissions.Sales.ChangePrice),
            User.HasPermission(Permissions.Sales.Discount),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<QuotationDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<QuotationDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/send")]
    [Authorize(Policy = Permissions.Quotations.Manage)]
    public Task<ActionResult<ApiResponse<QuotationDetailItem>>> Send(
        Guid id,
        CancellationToken cancellationToken) =>
        StatusResult(service.SendAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpPost("{id:guid}/accept")]
    [Authorize(Policy = Permissions.Quotations.Manage)]
    public Task<ActionResult<ApiResponse<QuotationDetailItem>>> Accept(
        Guid id,
        CancellationToken cancellationToken) =>
        StatusResult(service.AcceptAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Permissions.Quotations.Manage)]
    public Task<ActionResult<ApiResponse<QuotationDetailItem>>> Reject(
        Guid id,
        RejectQuotationRequest request,
        CancellationToken cancellationToken) =>
        StatusResult(service.RejectAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpPost("{id:guid}/convert")]
    [Authorize(Policy = Permissions.Quotations.Convert)]
    public async Task<ActionResult<ApiResponse<SaleDetailItem>>> Convert(
        Guid id,
        ConvertQuotationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ConvertAsync(
            id,
            request,
            User.GetRequiredUserId(),
            User.HasPermission(Permissions.Sales.SellOnDue),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SaleDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<SaleDetailItem>.Failure(result.Errors));
    }

    private async Task<ActionResult<ApiResponse<QuotationDetailItem>>> StatusResult(
        Task<OperationResult<QuotationDetailItem>> task)
    {
        var result = await task;
        return result.Succeeded
            ? Ok(ApiResponse<QuotationDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<QuotationDetailItem>.Failure(result.Errors));
    }
}
