using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Security;
using RetailShop.Application.Warranty;
using RetailShop.Domain.Warranty;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/warranty")]
[Authorize(Policy = Permissions.Warranty.View)]
public sealed class WarrantyController(IWarrantyService service) : ControllerBase
{
    [HttpGet("serials/available/{productId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ProductSerialItem>>>>
        AvailableSerials(Guid productId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<ProductSerialItem>>.Success(
            await service.GetAvailableSerialsAsync(productId, cancellationToken)));

    [HttpPost("serials")]
    [Authorize(Policy = Permissions.Inventory.OpeningStock)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ProductSerialItem>>>>
        RegisterSerials(
            RegisterProductSerialsRequest request,
            CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<ProductSerialItem>>.Success(
            await service.RegisterSerialsAsync(
                request,
                User.GetRequiredUserId(),
                cancellationToken)));

    [HttpGet("lookup/serial/{serialNumber}")]
    public async Task<ActionResult<ApiResponse<WarrantyLookupItem>>> LookupSerial(
        string serialNumber,
        CancellationToken cancellationToken)
    {
        var result = await service.LookupBySerialAsync(serialNumber, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<WarrantyLookupItem>.Success(result.Value!))
            : NotFound(ApiResponse<WarrantyLookupItem>.Failure(result.Errors));
    }

    [HttpGet("lookup/invoice/{invoiceNumber}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<WarrantyLookupItem>>>>
        LookupInvoice(string invoiceNumber, CancellationToken cancellationToken)
    {
        var result = await service.LookupByInvoiceAsync(invoiceNumber, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<IReadOnlyCollection<WarrantyLookupItem>>.Success(result.Value!))
            : NotFound(ApiResponse<IReadOnlyCollection<WarrantyLookupItem>>.Failure(result.Errors));
    }

    [HttpGet("claims")]
    public async Task<ActionResult<ApiResponse<PagedResult<WarrantyClaimListItem>>>>
        Claims(
            [FromQuery] string? search,
            [FromQuery] WarrantyClaimStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<WarrantyClaimListItem>>.Success(
            await service.GetClaimsAsync(
                search,
                status,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("claims/{id:guid}")]
    public async Task<ActionResult<ApiResponse<WarrantyClaimDetailItem>>> Claim(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetClaimAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<WarrantyClaimDetailItem>.Success(result.Value!))
            : NotFound(ApiResponse<WarrantyClaimDetailItem>.Failure(result.Errors));
    }

    [HttpPost("claims")]
    [Authorize(Policy = Permissions.Warranty.CreateClaim)]
    public async Task<ActionResult<ApiResponse<WarrantyClaimDetailItem>>> CreateClaim(
        CreateWarrantyClaimRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateClaimAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(Claim),
                new { id = result.Value!.Id },
                ApiResponse<WarrantyClaimDetailItem>.Success(result.Value))
            : BadRequest(ApiResponse<WarrantyClaimDetailItem>.Failure(result.Errors));
    }

    [HttpPost("claims/{id:guid}/approve")]
    [Authorize(Policy = Permissions.Warranty.ApproveClaim)]
    public async Task<ActionResult<ApiResponse<WarrantyClaimDetailItem>>> Approve(
        Guid id,
        ReviewWarrantyClaimRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<WarrantyClaimDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<WarrantyClaimDetailItem>.Failure(result.Errors));
    }

    [HttpPost("claims/{id:guid}/reject")]
    [Authorize(Policy = Permissions.Warranty.ApproveClaim)]
    public async Task<ActionResult<ApiResponse<WarrantyClaimDetailItem>>> Reject(
        Guid id,
        RejectWarrantyClaimRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<WarrantyClaimDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<WarrantyClaimDetailItem>.Failure(result.Errors));
    }

    [HttpPost("claims/{id:guid}/resolve")]
    [Authorize(Policy = Permissions.Warranty.ApproveClaim)]
    public async Task<ActionResult<ApiResponse<WarrantyClaimDetailItem>>> Resolve(
        Guid id,
        ResolveWarrantyClaimRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ResolveAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<WarrantyClaimDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<WarrantyClaimDetailItem>.Failure(result.Errors));
    }
}
