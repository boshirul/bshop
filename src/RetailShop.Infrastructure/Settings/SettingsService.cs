using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Settings;
using RetailShop.Domain.Settings;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Settings;

internal sealed class SettingsService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : ISettingsService
{
    private static readonly string[] PaymentTypes =
        ["Cash", "Card", "MobileBanking", "BankTransfer", "Other"];

    public async Task<ShopSettingsResponse> GetShopAsync(
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.ShopSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return setting is null
            ? new ShopSettingsResponse(
                "KhanShop", null, null, null, null, null,
                "Thank you for shopping with us.")
            : Map(setting);
    }

    public async Task<ShopSettingsResponse> SaveShopAsync(
        SaveShopSettingsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        if (setting is null)
        {
            setting = new ShopSetting { CreatedBy = performedBy };
            dbContext.ShopSettings.Add(setting);
        }
        else
        {
            setting.LastModifiedBy = performedBy;
        }

        setting.ShopName = request.ShopName.Trim();
        setting.Address = Clean(request.Address);
        setting.Phone = Clean(request.Phone);
        setting.Email = Clean(request.Email);
        setting.LogoUrl = Clean(request.LogoUrl);
        setting.TaxRegistrationNumber = Clean(request.TaxRegistrationNumber);
        setting.ReceiptFooter = request.ReceiptFooter.Trim();
        await SaveAndAuditAsync("ShopSetting", setting.Id, performedBy, cancellationToken);
        return Map(setting);
    }

    public async Task<InvoiceSettingsResponse> GetInvoiceAsync(
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.InvoiceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return setting is null
            ? new InvoiceSettingsResponse("INV", 1, null, null, true, true)
            : Map(setting);
    }

    public async Task<InvoiceSettingsResponse> SaveInvoiceAsync(
        SaveInvoiceSettingsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.InvoiceSettings.FirstOrDefaultAsync(cancellationToken);
        if (setting is null)
        {
            setting = new InvoiceSetting { CreatedBy = performedBy };
            dbContext.InvoiceSettings.Add(setting);
        }
        else
        {
            setting.LastModifiedBy = performedBy;
        }

        setting.InvoicePrefix = request.InvoicePrefix.Trim().ToUpperInvariant();
        setting.NextInvoiceNumber = request.NextInvoiceNumber;
        setting.TermsAndConditions = Clean(request.TermsAndConditions);
        setting.ReturnPolicy = Clean(request.ReturnPolicy);
        setting.ShowTaxDetails = request.ShowTaxDetails;
        setting.ShowQrCode = request.ShowQrCode;
        await SaveAndAuditAsync("InvoiceSetting", setting.Id, performedBy, cancellationToken);
        return Map(setting);
    }

    public async Task<TaxSettingsResponse> GetTaxAsync(
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.TaxSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return setting is null
            ? new TaxSettingsResponse("VAT", 0, false)
            : Map(setting);
    }

    public async Task<TaxSettingsResponse> SaveTaxAsync(
        SaveTaxSettingsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.TaxSettings.FirstOrDefaultAsync(cancellationToken);
        if (setting is null)
        {
            setting = new TaxSetting { CreatedBy = performedBy };
            dbContext.TaxSettings.Add(setting);
        }
        else
        {
            setting.LastModifiedBy = performedBy;
        }

        setting.TaxName = request.TaxName.Trim();
        setting.DefaultRate = request.DefaultRate;
        setting.IsEnabled = request.IsEnabled;
        await SaveAndAuditAsync("TaxSetting", setting.Id, performedBy, cancellationToken);
        return Map(setting);
    }

    public async Task<SystemSettingsResponse> GetSystemAsync(
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return setting is null
            ? new SystemSettingsResponse("BDT", "Asia/Dhaka", "dd MMM yyyy", 25, true)
            : Map(setting);
    }

    public async Task<SystemSettingsResponse> SaveSystemAsync(
        SaveSystemSettingsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (setting is null)
        {
            setting = new SystemSetting { CreatedBy = performedBy };
            dbContext.SystemSettings.Add(setting);
        }
        else
        {
            setting.LastModifiedBy = performedBy;
        }

        setting.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        setting.TimeZone = request.TimeZone.Trim();
        setting.DateFormat = request.DateFormat.Trim();
        setting.DefaultPageSize = request.DefaultPageSize;
        setting.LowStockAlertsEnabled = request.LowStockAlertsEnabled;
        await SaveAndAuditAsync("SystemSetting", setting.Id, performedBy, cancellationToken);
        return Map(setting);
    }

    public async Task<IReadOnlyCollection<PaymentMethodItem>> GetPaymentMethodsAsync(
        CancellationToken cancellationToken) =>
        await dbContext.PaymentMethods
            .AsNoTracking()
            .OrderBy(method => method.Name)
            .Select(method => new PaymentMethodItem(
                method.Id,
                method.Name,
                method.Code,
                method.Type,
                method.IsActive))
            .ToArrayAsync(cancellationToken);

    public async Task<OperationResult<PaymentMethodItem>> CreatePaymentMethodAsync(
        SavePaymentMethodRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var errors = await ValidatePaymentMethodAsync(null, request, cancellationToken);
        if (errors.Count > 0)
        {
            return OperationResult<PaymentMethodItem>.Failure(errors);
        }

        var method = new PaymentMethod(request.Name, request.Code, request.Type)
        {
            IsActive = request.IsActive,
            CreatedBy = performedBy
        };
        dbContext.PaymentMethods.Add(method);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Create", "PaymentMethod", method.Id, performedBy, cancellationToken);
        return OperationResult<PaymentMethodItem>.Success(Map(method));
    }

    public async Task<OperationResult<PaymentMethodItem>> UpdatePaymentMethodAsync(
        Guid id,
        SavePaymentMethodRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var method = await dbContext.PaymentMethods.FindAsync([id], cancellationToken);
        if (method is null)
        {
            return OperationResult<PaymentMethodItem>.Failure("Payment method not found.");
        }

        var errors = await ValidatePaymentMethodAsync(id, request, cancellationToken);
        if (errors.Count > 0)
        {
            return OperationResult<PaymentMethodItem>.Failure(errors);
        }

        method.Update(request.Name, request.Code, request.Type);
        method.IsActive = request.IsActive;
        method.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Update", "PaymentMethod", method.Id, performedBy, cancellationToken);
        return OperationResult<PaymentMethodItem>.Success(Map(method));
    }

    public async Task<OperationResult<bool>> DeletePaymentMethodAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var method = await dbContext.PaymentMethods.FindAsync([id], cancellationToken);
        if (method is null)
        {
            return OperationResult<bool>.Failure("Payment method not found.");
        }

        method.DeletedBy = performedBy;
        dbContext.PaymentMethods.Remove(method);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Delete", "PaymentMethod", method.Id, performedBy, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    private async Task<List<string>> ValidatePaymentMethodAsync(
        Guid? id,
        SavePaymentMethodRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (!PaymentTypes.Contains(request.Type, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Payment type must be one of: {string.Join(", ", PaymentTypes)}.");
        }

        var normalizedName = request.Name.Trim().ToUpperInvariant();
        var code = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.PaymentMethods.AnyAsync(
                method =>
                    (!id.HasValue || method.Id != id.Value) &&
                    (method.NormalizedName == normalizedName || method.Code == code),
                cancellationToken))
        {
            errors.Add("A payment method with this name or code already exists.");
        }
        return errors;
    }

    private async Task SaveAndAuditAsync(
        string entityType,
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Update", entityType, id, performedBy, cancellationToken);
    }

    private Task WriteAuditAsync(
        string action,
        string entityType,
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken) =>
        auditService.WriteAsync(
            action,
            entityType,
            id.ToString(),
            $"{action} {entityType}.",
            performedBy,
            cancellationToken);

    private static ShopSettingsResponse Map(ShopSetting setting) =>
        new(
            setting.ShopName,
            setting.Address,
            setting.Phone,
            setting.Email,
            setting.LogoUrl,
            setting.TaxRegistrationNumber,
            setting.ReceiptFooter);

    private static InvoiceSettingsResponse Map(InvoiceSetting setting) =>
        new(
            setting.InvoicePrefix,
            setting.NextInvoiceNumber,
            setting.TermsAndConditions,
            setting.ReturnPolicy,
            setting.ShowTaxDetails,
            setting.ShowQrCode);

    private static TaxSettingsResponse Map(TaxSetting setting) =>
        new(setting.TaxName, setting.DefaultRate, setting.IsEnabled);

    private static SystemSettingsResponse Map(SystemSetting setting) =>
        new(
            setting.CurrencyCode,
            setting.TimeZone,
            setting.DateFormat,
            setting.DefaultPageSize,
            setting.LowStockAlertsEnabled);

    private static PaymentMethodItem Map(PaymentMethod method) =>
        new(method.Id, method.Name, method.Code, method.Type, method.IsActive);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
