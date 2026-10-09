using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Contacts;
using RetailShop.Application.Security;
using RetailShop.Application.Purchases;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize(Policy = Permissions.Suppliers.View)]
public sealed class SuppliersController(
    ISupplierService service,
    IPurchaseService purchaseService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierListItem>>>> Get(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<SupplierListItem>>.Success(
            await service.GetAsync(search, isActive, page, pageSize, cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SupplierListItem>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SupplierListItem>.Success(result.Value!))
            : NotFound(ApiResponse<SupplierListItem>.Failure(result.Errors));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Suppliers.Manage)]
    public async Task<ActionResult<ApiResponse<SupplierListItem>>> Create(
        SaveSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(GetById),
                new { id = result.Value!.Id },
                ApiResponse<SupplierListItem>.Success(result.Value))
            : BadRequest(ApiResponse<SupplierListItem>.Failure(result.Errors));
    }

    [HttpGet("{id:guid}/ledger")]
    public async Task<ActionResult<ApiResponse<SupplierLedgerResult>>> Ledger(
        Guid id,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await purchaseService.GetSupplierLedgerAsync(
            id,
            from,
            to,
            page,
            pageSize,
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SupplierLedgerResult>.Success(result.Value!))
            : NotFound(ApiResponse<SupplierLedgerResult>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Suppliers.Manage)]
    public async Task<ActionResult<ApiResponse<SupplierListItem>>> Update(
        Guid id,
        SaveSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SupplierListItem>.Success(result.Value!))
            : BadRequest(ApiResponse<SupplierListItem>.Failure(result.Errors));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Suppliers.Manage)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }
}
