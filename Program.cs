using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Pr3.ConfigAndSecurity.Config;
using Pr3.ConfigAndSecurity.Domain;
using Pr3.ConfigAndSecurity.Middlewares;
using Pr3.ConfigAndSecurity.Services;

var switchMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["--mode"] = "App:Mode",
    ["--origins"] = "App:TrustedOrigins",
    ["--readPerMinute"] = "App:RateLimits:ReadPerMinute",
    ["--writePerMinute"] = "App:RateLimits:WritePerMinute"
};

var builder = WebApplication.CreateBuilder(args);

// Явный порядок источников настроек
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "PR3_")
    .AddCommandLine(args, switchMappings);

// Чтение и ранняя проверка настроек
var options = new AppOptions();
builder.Configuration.GetSection("App").Bind(options);

var errors = AppOptionsValidator.Validate(options);
if (errors.Count > 0)
{
    var text = string.Join(Environment.NewLine, errors.Select(e => "- " + e));
    Console.Error.WriteLine("Запуск остановлен из-за некорректных настроек");
    Console.Error.WriteLine(text);
    throw new InvalidOperationException("Некорректные настройки приложения");
}

Console.WriteLine($"✅ Режим: {options.Mode}");
Console.WriteLine($"✅ Доверенные источники: {string.Join(", ", options.TrustedOrigins)}");
Console.WriteLine($"✅ RateLimit чтение: {options.RateLimits.ReadPerMinute}/мин");
Console.WriteLine($"✅ RateLimit запись: {options.RateLimits.WritePerMinute}/мин");

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IItemRepository, InMemoryItemRepository>();

// ДОБАВИТЬ SWAGGER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(cors =>
{
    cors.AddPolicy("TrustedOrigins", policy =>
    {
        policy.WithOrigins(options.TrustedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// RateLimiter
builder.Services.AddRateLimiter(limiter =>
{
    limiter.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Слишком много запросов", token);
    };

    limiter.AddPolicy("read", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.RateLimits.ReadPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }
        );
    });

    limiter.AddPolicy("write", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.RateLimits.WritePerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }
        );
    });
});

var app = builder.Build();

// ДОБАВИТЬ SWAGGER UI
// Всегда включаем Swagger для тестирования
app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors("TrustedOrigins");
app.UseRateLimiter();
app.UseMiddleware<ErrorHandlingMiddleware>();

// API Endpoints
app.MapGet("/api/items", (IItemRepository repo) => Results.Ok(repo.GetAll()))
    .RequireRateLimiting("read");

app.MapGet("/api/items/by-id/{id:guid}", (Guid id, IItemRepository repo) =>
{
    var item = repo.GetById(id);
    if (item is null) throw new ArgumentException("Элемент не найден");
    return Results.Ok(item);
}).RequireRateLimiting("read");

app.MapPost("/api/items", (HttpContext ctx, CreateItemRequest request, IItemRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        throw new ArgumentException("Поле name не должно быть пустым");
    if (request.Price < 0)
        throw new ArgumentException("Поле price не может быть отрицательным");

    var created = repo.Create(request.Name.Trim(), request.Price);
    ctx.Response.Headers.Location = $"/api/items/by-id/{created.Id}";
    return Results.Created($"/api/items/by-id/{created.Id}", created);
}).RequireRateLimiting("write");

app.MapGet("/api/mode", (AppOptions o) => Results.Ok(new { mode = o.Mode.ToString() }))
    .RequireRateLimiting("read");

app.MapGet("/api/headers-check", (HttpContext ctx) =>
{
    return Results.Ok(new
    {
        XContentTypeOptions = ctx.Response.Headers["X-Content-Type-Options"].ToString(),
        XFrameOptions = ctx.Response.Headers["X-Frame-Options"].ToString()
    });
}).RequireRateLimiting("read");

app.MapGet("/api/load-test", async (HttpContext ctx) =>
{
    await Task.Delay(50);
    return Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow });
}).RequireRateLimiting("read");

app.Run();

public partial class Program { }
