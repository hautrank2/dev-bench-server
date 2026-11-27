using System.Text.Json;
using System.Threading.RateLimiting;
using DevBench.Api.Configuration;
using DevBench.Api.Controllers;
using DevBench.Api.Middlewares;
using DevBench.Api.Repositories;
using DevBench.Api.Services.Interfaces;
using DevBench.Api.Services;
using DevBench.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace DevBench.Api;

public static class ApiApplication
{
    public static async Task<WebApplication> BuildAsync(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = BackupValidator.MaxBytes);
        builder.Services.AddSingleton(services => ApiOptions.Read(services.GetRequiredService<IConfiguration>()));
        builder.Services.AddSingleton<ICloudStore, MongoCloudStore>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHttpClient<IGoogleProvider, GoogleProvider>(client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<ISessionService, SessionService>();
        builder.Services.AddScoped<IBackupService, BackupService>();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new { error = "Invalid request" });
                options.SuppressMapClientErrors = true;
            });
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSwaggerGen(options =>
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "DevBench API", Version = "v1" }));
        }
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
        if (app.Environment.IsDevelopment())
        {
            // Swagger UI serves scripts and styles; keep it outside the API's restrictive CSP.
            app.UseSwagger();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("v1/swagger.json", "DevBench API v1"));
        }
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseRateLimiter();
        app.MapControllers();
        return app;
    }
}
