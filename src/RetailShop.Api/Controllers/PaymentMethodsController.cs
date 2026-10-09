using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Security;
using RetailShop.Application.Settings;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/payment-methods")]
[Authorize(Policy = Permissions.Settings.View)]
public sealed class PaymentMethodsController(ISettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PaymentMethodItem>>>> Get(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<PaymentMethodItem>>.Success(
            await service.GetPaymentMethodsAsync(cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Permissions.Settings.Manage)]
    public async Task<ActionResult<ApiResponse<PaymentMethodItem>>> Create(
        SavePaymentMethodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreatePaymentMethodAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<PaymentMethodItem>.Success(result.Value!))
            : BadRequest(ApiResponse<PaymentMethodItem>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Settings.Manage)]
    public async Task<ActionResult<ApiResponse<PaymentMethodItem>>> Update(
        Guid id,
        SavePaymentMethodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdatePaymentMethodAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<PaymentMethodItem>.Success(result.Value!))
            : BadRequest(ApiResponse<PaymentMethodItem>.Failure(result.Errors));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Settings.Manage)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeletePaymentMethodAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }
}
