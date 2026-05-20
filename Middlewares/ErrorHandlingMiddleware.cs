using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pr3.ConfigAndSecurity.Config;
using Pr3.ConfigAndSecurity.Domain;

namespace Pr3.ConfigAndSecurity.Middlewares;

public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly AppOptions _options;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, AppOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }
      public async Task Invoke(HttpContext context)
    {
        var requestId = RequestId.GetOrCreate(context);

        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Ошибка входных данных. requestId={RequestId}", requestId);
            await WriteError(context, 400, "bad_request", ToClientMessage(ex.Message), requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непредвиденная ошибка. requestId={RequestId}", requestId);
            await WriteError(context, 500, "internal_error", ToClientMessage("Внутренняя ошибка сервера"), requestId);
        }
    }

    private string ToClientMessage(string msg)
    {
        // Учебный режим -> подробное сообщение
        // Боевой режим -> краткое сообщение
        return _options.Mode == AppMode.Учебный ? msg : "Ошибка обработки запроса";
    }

    private static async Task WriteError(HttpContext context, int statusCode, string code, string message, string requestId)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        // Добавляем защитные заголовки при ошибке
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        var payload = new ErrorResponse(code, message, requestId);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
