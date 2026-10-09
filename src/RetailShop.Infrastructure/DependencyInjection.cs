using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RetailShop.Application.Administration;
using RetailShop.Application.Auditing;
using RetailShop.Application.Authentication;
using RetailShop.Application.Security;
using RetailShop.Infrastructure.Administration;
using RetailShop.Infrastructure.Auditing;
using RetailShop.Infrastructure.Identity;
using RetailShop.Infrastructure.Persistence;
using RetailShop.Application.Products;
using RetailShop.Application.Contacts;
using RetailShop.Application.Settings;
using RetailShop.Application.Inventory;
using RetailShop.Infrastructure.Contacts;
using RetailShop.Infrastructure.Products;
using RetailShop.Infrastructure.Settings;
using RetailShop.Infrastructure.Inventory;
using RetailShop.Application.Purchases;
using RetailShop.Infrastructure.Purchases;
using RetailShop.Application.Sales;
using RetailShop.Infrastructure.Sales;
using RetailShop.Application.Quotations;
using RetailShop.Infrastructure.Quotations;
using RetailShop.Application.CustomerAccounts;
using RetailShop.Infrastructure.CustomerAccounts;
using RetailShop.Application.Returns;
using RetailShop.Infrastructure.Returns;
using RetailShop.Application.Warranty;
using RetailShop.Infrastructure.Warranty;
using RetailShop.Application.OnlineOrders;
using RetailShop.Infrastructure.OnlineOrders;
using RetailShop.Application.Reports;
using RetailShop.Infrastructure.Reports;
using RetailShop.Application.DataExchange;
using RetailShop.Infrastructure.DataExchange;
using RetailShop.Application.Notifications;
using RetailShop.Infrastructure.Notifications;

namespace RetailShop.Infrastructure;

public static class DependencyInjection
{
    private const string DatabaseConnectionName = "RetailShopDatabase";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConnectionName)
            ?? throw new InvalidOperationException(
                $"Connection string '{DatabaseConnectionName}' is not configured.");

        services.AddDbContext<RetailShopDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(RetailShopDbContext).Assembly.FullName)));

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwt = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");
        if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must contain at least 32 bytes.");
        }

        services
            .AddOptions<JwtOptions>()
            .Bind(jwtSection)
            .Validate(options => Encoding.UTF8.GetByteCount(options.Key) >= 32)
            .ValidateOnStart();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<RetailShopDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = CustomClaimTypes.FullName,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(
                    permission.Name,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireClaim(CustomClaimTypes.Permission, permission.Name));
            }
        });

        services.AddScoped<TokenService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPasswordResetNotifier, LoggingPasswordResetNotifier>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<IProductMasterDataService, ProductMasterDataService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<ICustomerAccountService, CustomerAccountService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<IWarrantyService, WarrantyService>();
        services.AddScoped<IOnlineOrderService, OnlineOrderService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDataExchangeService, DataExchangeService>();
        services.AddScoped<INotificationJobService, NotificationJobService>();

        return services;
    }
}
