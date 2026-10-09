using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.CustomerAccounts;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/customer-accounts")]
[Authorize(Policy = Permissions.Customers.ViewAccounts)]
public sealed class CustomerAccountsController(
    ICustomerAccountService service) : ControllerBase
{
    [HttpGet("dues")]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerDueSummaryItem>>>> Dues(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<CustomerDueSummaryItem>>.Success(
            await service.GetDuesAsync(
                search,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("aging")]
    public async Task<ActionResult<ApiResponse<CustomerAgingResult>>> Aging(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<CustomerAgingResult>.Success(
            await service.GetAgingAsync(cancellationToken)));

    [HttpGet("receipts")]
    public async Task<
        ActionResult<ApiResponse<PagedResult<CustomerReceiptListItem>>>> Receipts(
        [FromQuery] string? search,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<CustomerReceiptListItem>>.Success(
            await service.GetReceiptsAsync(
                search,
                customerId,
                from,
                to,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("receipts/{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerReceiptDetailItem>>> Receipt(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetReceiptAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<CustomerReceiptDetailItem>.Success(result.Value!))
            : NotFound(ApiResponse<CustomerReceiptDetailItem>.Failure(result.Errors));
    }

    [HttpPost("receipts")]
    [Authorize(Policy = Permissions.Customers.CollectDue)]
    public async Task<ActionResult<ApiResponse<CustomerReceiptDetailItem>>> CreateReceipt(
        CreateCustomerReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateReceiptAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(Receipt),
                new { id = result.Value!.Id },
                ApiResponse<CustomerReceiptDetailItem>.Success(result.Value))
            : BadRequest(ApiResponse<CustomerReceiptDetailItem>.Failure(result.Errors));
    }

    [HttpPost("adjustments")]
    [Authorize(Policy = Permissions.Customers.AdjustLedger)]
    public async Task<ActionResult<ApiResponse<CustomerStatementResult>>> Adjustment(
        CreateCustomerAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAdjustmentAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<CustomerStatementResult>.Success(result.Value!))
            : BadRequest(ApiResponse<CustomerStatementResult>.Failure(result.Errors));
    }
}
