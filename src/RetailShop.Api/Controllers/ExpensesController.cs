using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Expenses;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize(Policy = Permissions.Expenses.View)]
public sealed class ExpensesController(IExpenseService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ExpenseVoucherListItem>>>> Get(
        [FromQuery] string? search, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<ExpenseVoucherListItem>>.Success(
            await service.GetVouchersAsync(search, from, to, page, pageSize, cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherItem>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetVoucherAsync(id, cancellationToken);
        return result.Succeeded ? Ok(ApiResponse<ExpenseVoucherItem>.Success(result.Value!)) : NotFound(ApiResponse<ExpenseVoucherItem>.Failure(result.Errors));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Expenses.Manage)]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherItem>>> Create(SaveExpenseVoucherRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, ApiResponse<ExpenseVoucherItem>.Success(result.Value)) : BadRequest(ApiResponse<ExpenseVoucherItem>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Expenses.Manage)]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherItem>>> Update(Guid id, SaveExpenseVoucherRequest request, CancellationToken cancellationToken) =>
        Result(await service.UpdateAsync(id, request, User.GetRequiredUserId(), cancellationToken));

    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = Permissions.Expenses.Post)]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherItem>>> Post(Guid id, CancellationToken cancellationToken) =>
        Result(await service.PostAsync(id, User.GetRequiredUserId(), cancellationToken));

    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Permissions.Expenses.Void)]
    public async Task<ActionResult<ApiResponse<ExpenseVoucherItem>>> Void(Guid id, [FromBody] VoidExpenseVoucherRequest request, CancellationToken cancellationToken) =>
        Result(await service.VoidAsync(id, request.Reason, User.GetRequiredUserId(), cancellationToken));

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ExpenseCategoryItem>>>> Categories(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<ExpenseCategoryItem>>.Success(await service.GetCategoriesAsync(cancellationToken)));

    [HttpPost("categories")]
    [Authorize(Policy = Permissions.Expenses.ManageCategories)]
    public async Task<ActionResult<ApiResponse<ExpenseCategoryItem>>> CreateCategory(SaveExpenseCategoryRequest request, CancellationToken cancellationToken) =>
        CategoryResult(await service.SaveCategoryAsync(null, request, User.GetRequiredUserId(), cancellationToken));

    [HttpPut("categories/{id:guid}")]
    [Authorize(Policy = Permissions.Expenses.ManageCategories)]
    public async Task<ActionResult<ApiResponse<ExpenseCategoryItem>>> UpdateCategory(Guid id, SaveExpenseCategoryRequest request, CancellationToken cancellationToken) =>
        CategoryResult(await service.SaveCategoryAsync(id, request, User.GetRequiredUserId(), cancellationToken));

    private static ActionResult<ApiResponse<ExpenseVoucherItem>> Result(OperationResult<ExpenseVoucherItem> result) =>
        result.Succeeded ? new OkObjectResult(ApiResponse<ExpenseVoucherItem>.Success(result.Value!)) : new BadRequestObjectResult(ApiResponse<ExpenseVoucherItem>.Failure(result.Errors));

    private static ActionResult<ApiResponse<ExpenseCategoryItem>> CategoryResult(OperationResult<ExpenseCategoryItem> result) =>
        result.Succeeded ? new OkObjectResult(ApiResponse<ExpenseCategoryItem>.Success(result.Value!)) : new BadRequestObjectResult(ApiResponse<ExpenseCategoryItem>.Failure(result.Errors));
}

public sealed record VoidExpenseVoucherRequest(string Reason);
