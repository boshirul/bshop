using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Contacts;
using RetailShop.Application.Security;
using RetailShop.Application.Sales;
using RetailShop.Application.CustomerAccounts;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Policy = Permissions.Customers.View)]
public sealed class CustomersController(
    ICustomerService service,
    ISaleService saleService,
    ICustomerAccountService accountService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerListItem>>>> Get(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<CustomerListItem>>.Success(
            await service.GetAsync(search, isActive, page, pageSize, cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerListItem>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<CustomerListItem>.Success(result.Value!))
            : NotFound(ApiResponse<CustomerListItem>.Failure(result.Errors));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Customers.Manage)]
    public async Task<ActionResult<ApiResponse<CustomerListItem>>> Create(
        SaveCustomerRequest request,
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
                ApiResponse<CustomerListItem>.Success(result.Value))
            : BadRequest(ApiResponse<CustomerListItem>.Failure(result.Errors));
    }

    [HttpGet("{id:guid}/statement")]
    [Authorize(Policy = Permissions.Customers.PrintStatement)]
    public async Task<ActionResult<ApiResponse<CustomerStatementResult>>> Statement(
        Guid id,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var result = await accountService.GetStatementAsync(
            id,
            from,
            to,
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<CustomerStatementResult>.Success(result.Value!))
            : NotFound(ApiResponse<CustomerStatementResult>.Failure(result.Errors));
    }

    [HttpGet("{id:guid}/ledger")]
    public async Task<ActionResult<ApiResponse<CustomerLedgerResult>>> Ledger(
        Guid id,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await saleService.GetCustomerLedgerAsync(
            id,
            from,
            to,
            page,
            pageSize,
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<CustomerLedgerResult>.Success(result.Value!))
            : NotFound(ApiResponse<CustomerLedgerResult>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Customers.Manage)]
    public async Task<ActionResult<ApiResponse<CustomerListItem>>> Update(
        Guid id,
        SaveCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<CustomerListItem>.Success(result.Value!))
            : BadRequest(ApiResponse<CustomerListItem>.Failure(result.Errors));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Customers.Manage)]
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
