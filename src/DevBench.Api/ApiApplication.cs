using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace DevBench.Api;

public static class ApiApplication
{
    public static string Hash(string text) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string Secret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static async Task<WebApplication> BuildAsync(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = BackupValidator.MaxBytes);
        builder.Services.AddSingleton(services => ApiOptions.Read(services.GetRequiredService<IConfiguration>()));
        builder.Services.AddSingleton<ICloudStore, MongoCloudStore>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHttpClient<IGoogleProvider, GoogleProvider>(client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new() { PermitLimit = 100, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, cancellation) => await context.HttpContext.Response.WriteAsJsonAsync(
                new { error = "Too many requests. Try again shortly." }, cancellation);
        });
        configure?.Invoke(builder);
        var app = builder.Build();
        // Fail startup on missing configuration or database/index errors, rather than accepting unusable requests.
        _ = app.Services.GetRequiredService<ApiOptions>();
        await app.Services.GetRequiredService<ICloudStore>().InitializeAsync();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            try { await next(context); }
            catch (Exception exception) when (!context.Response.HasStarted &&
                exception is ArgumentException or JsonException or BadHttpRequestException)
            {
                var status = exception is BadHttpRequestException bad ? bad.StatusCode : 400;
                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(new { error = status == 413 ? "Backup exceeds the 2 MB limit" : "Invalid request" });
            }
            catch (Exception exception) when (!context.Response.HasStarted)
            {
                // Avoid logging OAuth codes, bearer tokens or database connection details.
                app.Logger.LogError("Request failed: {ExceptionType}", exception.GetType().Name);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Request failed. Try again or contact your server administrator." });
            }
        });
        app.UseRateLimiter();
        AuthEndpoints.Map(app);
        BackupEndpoints.Map(app);
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        return app;
    }

    public static async Task<SessionRecord?> SessionAsync(HttpContext context, ICloudStore store, TimeProvider clock)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var token = header[7..];
        return token.Length is 0 or > 256 ? null : await store.FindSessionAsync(Hash(token), clock.GetUtcNow().UtcDateTime);
    }
    public static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);
}
