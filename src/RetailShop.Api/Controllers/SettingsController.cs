using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Security;
using RetailShop.Application.Settings;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Policy = Permissions.Settings.View)]
public sealed class SettingsController(ISettingsService service) : ControllerBase
{
    [HttpGet("shop")]
    public async Task<ActionResult<ApiResponse<ShopSettingsResponse>>> GetShop(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<ShopSettingsResponse>.Success(
            await service.GetShopAsync(cancellationToken)));

    [HttpPut("shop")]
    [Authorize(Policy = Permissions.Settings.Manage)]
    public async Task<ActionResult<ApiResponse<ShopSettingsResponse>>> SaveShop(
        SaveShopSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<ShopSettingsResponse>.Success(
            await service.SaveShopAsync(
                request,
                User.GetRequiredUserId(),
                cancellationToken)));

    [HttpGet("invoice")]
    public async Task<ActionResult<ApiResponse<InvoiceSettingsResponse>>> GetInvoice(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<InvoiceSettingsResponse>.Success(
            await service.GetInvoiceAsync(cancellationToken)));

    [HttpPut("invoice")]
    [Authorize(Policy = Permissions.Settings.Manage)]
    public async Task<ActionResult<ApiResponse<InvoiceSettingsResponse>>> SaveInvoice(
        SaveInvoiceSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<InvoiceSettingsResponse>.Success(
            await service.SaveInvoiceAsync(
                request,
                User.GetRequiredUserId(),
                cancellationToken)));

    [HttpGet("tax")]
    public async Task<ActionResult<ApiResponse<TaxSettingsResponse>>> GetTax(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<TaxSettingsResponse>.Success(
            await service.GetTaxAsync(cancellationToken)));

    [HttpPut("tax")]
    [Authorize(Policy = Permissions.Settings.Manage)]
    public async Task<ActionResult<ApiResponse<TaxSettingsResponse>>> SaveTax(
        SaveTaxSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<TaxSettingsResponse>.Success(
            await service.SaveTaxAsync(
                request,
                User.GetRequiredUserId(),
                cancellationToken)));

    [HttpGet("system")]
    public async Task<ActionResult<ApiResponse<SystemSettingsResponse>>> GetSystem(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<SystemSettingsResponse>.Success(
            await service.GetSystemAsync(cancellationToken)));

    [HttpPut("system")]
    [Authorize(Policy = Permissions.Settings.Manage)]
    public async Task<ActionResult<ApiResponse<SystemSettingsResponse>>> SaveSystem(
        SaveSystemSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<SystemSettingsResponse>.Success(
            await service.SaveSystemAsync(
                request,
                User.GetRequiredUserId(),
                cancellationToken)));
}
