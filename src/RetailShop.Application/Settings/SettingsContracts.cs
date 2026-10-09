using System.ComponentModel.DataAnnotations;
using RetailShop.Application.Common;

namespace RetailShop.Application.Settings;

public sealed record ShopSettingsResponse(
    string ShopName,
    string? Address,
    string? Phone,
    string? Email,
    string? LogoUrl,
    string? TaxRegistrationNumber,
    string ReceiptFooter);

public sealed record SaveShopSettingsRequest(
    [param: Required, MaxLength(200)] string ShopName,
    [param: MaxLength(500)] string? Address,
    [param: MaxLength(30)] string? Phone,
    [param: EmailAddress, MaxLength(256)] string? Email,
    [param: MaxLength(1000)] string? LogoUrl,
    [param: MaxLength(100)] string? TaxRegistrationNumber,
    [param: Required, MaxLength(500)] string ReceiptFooter);

public sealed record InvoiceSettingsResponse(
    string InvoicePrefix,
    int NextInvoiceNumber,
    string? TermsAndConditions,
    string? ReturnPolicy,
    bool ShowTaxDetails,
    bool ShowQrCode);

public sealed record SaveInvoiceSettingsRequest(
    [param: Required, MaxLength(20)] string InvoicePrefix,
    [param: Range(1, int.MaxValue)] int NextInvoiceNumber,
    [param: MaxLength(2000)] string? TermsAndConditions,
    [param: MaxLength(1000)] string? ReturnPolicy,
    bool ShowTaxDetails,
    bool ShowQrCode);

public sealed record TaxSettingsResponse(
    string TaxName,
    decimal DefaultRate,
    bool IsEnabled);

public sealed record SaveTaxSettingsRequest(
    [param: Required, MaxLength(50)] string TaxName,
    [param: Range(0, 100)] decimal DefaultRate,
    bool IsEnabled);

public sealed record SystemSettingsResponse(
    string CurrencyCode,
    string TimeZone,
    string DateFormat,
    int DefaultPageSize,
    bool LowStockAlertsEnabled);

public sealed record SaveSystemSettingsRequest(
    [param: Required, StringLength(3, MinimumLength = 3)] string CurrencyCode,
    [param: Required, MaxLength(100)] string TimeZone,
    [param: Required, MaxLength(50)] string DateFormat,
    [param: Range(10, 100)] int DefaultPageSize,
    bool LowStockAlertsEnabled);

public sealed record PaymentMethodItem(
    Guid Id,
    string Name,
    string Code,
    string Type,
    bool IsActive);

public sealed record SavePaymentMethodRequest(
    [param: Required, MaxLength(100)] string Name,
    [param: Required, MaxLength(30)] string Code,
    [param: Required, MaxLength(50)] string Type,
    bool IsActive = true);

public interface ISettingsService
{
    Task<ShopSettingsResponse> GetShopAsync(CancellationToken cancellationToken);
    Task<ShopSettingsResponse> SaveShopAsync(
        SaveShopSettingsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<InvoiceSettingsResponse> GetInvoiceAsync(CancellationToken cancellationToken);
    Task<InvoiceSettingsResponse> SaveInvoiceAsync(
        SaveInvoiceSettingsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<TaxSettingsResponse> GetTaxAsync(CancellationToken cancellationToken);
    Task<TaxSettingsResponse> SaveTaxAsync(
        SaveTaxSettingsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<SystemSettingsResponse> GetSystemAsync(CancellationToken cancellationToken);
    Task<SystemSettingsResponse> SaveSystemAsync(
        SaveSystemSettingsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PaymentMethodItem>> GetPaymentMethodsAsync(
        CancellationToken cancellationToken);
    Task<OperationResult<PaymentMethodItem>> CreatePaymentMethodAsync(
        SavePaymentMethodRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<PaymentMethodItem>> UpdatePaymentMethodAsync(
        Guid id,
        SavePaymentMethodRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeletePaymentMethodAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);
}
