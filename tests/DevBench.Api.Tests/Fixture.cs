using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DevBench.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevBench.Api.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class FakeGoogle : IGoogleProvider
{
    public string AuthorizationUrl(string state, string nonce, string challenge) => QueryHelpers.AddQueryString(
        "https://accounts.google.com/test", new Dictionary<string, string?>
            { ["state"] = state, ["nonce"] = nonce, ["code_challenge"] = challenge });
    public Task<GoogleIdentity> ExchangeAsync(string code, string verifier, string nonce, CancellationToken cancellationToken)
    {
        if (code == "invalid" || verifier.Length != 43 || nonce.Length != 43) throw new ArgumentException("Rejected identity");
        return Task.FromResult(new GoogleIdentity(code, $"{code}@gmail.com", code));
    }
}

public sealed class Fixture(WebApplication app, HttpClient http, ICloudStore store, TestClock clock) : IAsyncDisposable
{
    public HttpClient Http { get; } = http;
    public ICloudStore Store { get; } = store;
    public TestClock Clock { get; } = clock;
    public ApiOptions Options => app.Services.GetRequiredService<ApiOptions>();
    public static async Task<Fixture> StartAsync(string? url = null)
    {
        var mongoUri = Environment.GetEnvironmentVariable("TEST_MONGODB_URI");
        var options = new ApiOptions("http://localhost:4318", mongoUri ?? "mongodb://localhost:27017",
            $"dev_bench_test_{Guid.NewGuid():N}", "test-client", "test-secret");
        ICloudStore store = mongoUri is null ? new MemoryCloudStore() : new MongoCloudStore(options);
        var clock = new TestClock();
        var app = await ApiApplication.BuildAsync([], builder =>
        {
            builder.WebHost.UseUrls(url ?? "http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton(store);
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddSingleton<IGoogleProvider, FakeGoogle>();
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new(app, new HttpClient { BaseAddress = new Uri(address) }, store, clock);
    }
    public async Task<JsonObject> StartLoginAsync(string verifier)
    {
        var response = await Http.PostAsJsonAsync("/auth/desktop/start", new
            { challenge = ApiApplication.Hash(verifier), deviceId = Guid.NewGuid().ToString(), deviceName = "Work laptop" });
        Check.Status(response, 200);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }
    public async Task<HttpResponseMessage> CallbackAsync(JsonObject start, string code)
    {
        var url = new Uri(start["authorizationUrl"]!.GetValue<string>());
        var state = QueryHelpers.ParseQuery(url.Query)["state"].ToString();
        return await Http.GetAsync($"/auth/google/callback?state={Uri.EscapeDataString(state)}&code={Uri.EscapeDataString(code)}");
    }
    public async Task<HttpResponseMessage> CompleteAsync(JsonObject start, string verifier) =>
        await Http.PostAsJsonAsync("/auth/desktop/complete", new { loginId = start["loginId"]!.GetValue<string>(), verifier });
    public async Task<(string Token, string UserId)> SignInAsync(string user)
    {
        var verifier = ApiApplication.Secret();
        var start = await StartLoginAsync(verifier);
        Check.Status(await CallbackAsync(start, user), 200);
        var response = await CompleteAsync(start, verifier);
        Check.Status(response, 200);
        var json = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        return (json["token"]!.GetValue<string>(), json["account"]!["id"]!.GetValue<string>());
    }
    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, JsonObject? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await Http.SendAsync(request);
    }
    public async ValueTask DisposeAsync()
    {
        var options = app.Services.GetRequiredService<ApiOptions>();
        Http.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
        // Every fixture uses its own database. Production/user databases are never touched.
        if (Environment.GetEnvironmentVariable("TEST_MONGODB_URI") is string uri)
        {
            if (options.Database.StartsWith("dev_bench_test_", StringComparison.Ordinal))
                await new MongoDB.Driver.MongoClient(uri).DropDatabaseAsync(options.Database);
        }
    }
}

public static class Check
{
    public static void That(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Status(HttpResponseMessage response, int status) =>
        That((int)response.StatusCode == status, $"Expected HTTP {status}, received {(int)response.StatusCode}: {response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}");
}
