namespace RetailShop.Api.Infrastructure;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "no-referrer");
        headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

        await next(context);
    }
}
