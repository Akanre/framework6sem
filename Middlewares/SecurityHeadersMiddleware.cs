using Microsoft.AspNetCore.Http;

namespace Pr3.ConfigAndSecurity.Middlewares;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Cache-Control"] = "no-store, max-age=0";
        context.Response.Headers["Pragma"] = "no-cache";

        await _next(context);
    }
}
