using System.Text.Json;
using DevBench.Api.Services;

namespace DevBench.Api.Middlewares;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        try { await next(context); }
        catch (ApiException exception) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = exception.StatusCode;
            await context.Response.WriteAsJsonAsync(new { error = exception.Message });
        }
        catch (Exception exception) when (!context.Response.HasStarted &&
            exception is ArgumentException or JsonException or BadHttpRequestException)
        {
            var status = exception is BadHttpRequestException bad ? bad.StatusCode : 400;
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { error = status == 413 ? "Backup exceeds the 2 MB limit" : "Invalid request" });
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            // Never log OAuth codes, tokens or database connection details.
            logger.LogError("Request failed: {ExceptionType}", exception.GetType().Name);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "Request failed. Try again or contact your server administrator." });
        }
    }
}
