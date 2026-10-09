using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using RetailShop.Api.Infrastructure;
using RetailShop.Application;
using RetailShop.Application.Notifications;
using RetailShop.Infrastructure;
using RetailShop.Reporting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? [];
var dataProtectionKeysPath = Path.GetFullPath(
    builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, ".keys"));

builder.Services
    .AddDataProtection()
    .SetApplicationName("KhanShop")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddReporting();
var databaseConnectionString =
    builder.Configuration.GetConnectionString("RetailShopDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'RetailShopDatabase' is not configured.");
builder.Services.AddHangfire(configuration => configuration
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(databaseConnectionString)));
builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"KhanShop-{Environment.MachineName}";
    options.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2);
});

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("Authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("PublicStore", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("Checkout", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));
});
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard(
    "/jobs",
    new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthorizationFilter()],
        DashboardTitle = "KhanShop Background Jobs"
    });

app.MapControllers();

if (app.Configuration.GetValue<bool>("Database:InitialiseOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer =
        scope.ServiceProvider.GetRequiredService<
            RetailShop.Infrastructure.Persistence.IDatabaseInitializer>();
    await initializer.InitialiseAsync();
}

RecurringJob.AddOrUpdate<INotificationJobService>(
    "khanshop-notifications-daily",
    service => service.GenerateDailyNotificationsAsync(),
    Cron.Daily);
RecurringJob.AddOrUpdate<INotificationJobService>(
    "khanshop-low-stock-hourly",
    service => service.GenerateLowStockAlertsAsync(),
    Cron.Hourly);

app.Run();

public partial class Program;
