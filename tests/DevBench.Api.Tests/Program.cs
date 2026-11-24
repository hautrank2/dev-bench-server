using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DevBench.Api;
using DevBench.Api.Tests;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

if (args.Contains("--serve"))
{
    await using var server = await Fixture.StartAsync("http://127.0.0.1:14318");
    Console.WriteLine("TEST HARNESS ONLY: http://127.0.0.1:14318 (fake Google; no real credentials). Ctrl+C to stop.");
    var stopped = new TaskCompletionSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopped.TrySetResult(); };
    await stopped.Task;
    return;
}

var failures = 0;
async Task Test(string name, Func<Task> action)
{
    try { await action(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL {name}: {error}"); }
}

JsonObject Snapshot(int version = 2)
{
    var group = Guid.NewGuid().ToString();
    var repo = new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["groupId"] = group, ["name"] = "Client",
        ["folder"] = "D:\\Work App", ["command"] = "pnpm dev", ["url"] = "http://localhost:3000", ["includeInRunAll"] = true };
    return new JsonObject { ["kind"] = "dev-bench-project-set", ["schemaVersion"] = version,
        ["exportedAt"] = "2026-11-24T00:00:00.000Z", ["sourceDevice"] = "Work laptop", ["platform"] = "win32",
        ["appearance"] = new JsonObject { ["theme"] = "dark", ["terminalTheme"] = "rose" },
        ["set"] = version == 1
            ? new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["name"] = "Work", ["projects"] = new JsonArray(repo) }
            : new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["name"] = "Work",
                ["projects"] = new JsonArray(new JsonObject { ["id"] = group, ["name"] = "OpsDesk", ["description"] = "" }),
                ["repositories"] = new JsonArray(repo) } };
}

await Test("health, security headers and authentication required", async () =>
{
    await using var f = await Fixture.StartAsync();
    var health = await f.Http.GetAsync("/health");
    Check.Status(health, 200);
    Check.That(health.Headers.CacheControl?.NoStore == true, "Missing no-store");
    Check.That(health.Headers.Contains("Content-Security-Policy"), "Missing CSP");
    Check.Status(await f.Http.GetAsync("/me"), 401);
    Check.Status(await f.Http.GetAsync("/backups"), 401);
    Check.Status(await f.Http.PostAsJsonAsync("/backups", Snapshot()), 401);
    Check.Status(await f.Http.GetAsync($"/backups/{Guid.NewGuid()}"), 401);
});
await Test("PKCE, pending login and single-use state/session exchange", async () =>
{
    await using var f = await Fixture.StartAsync();
    var verifier = ApiApplication.Secret();
    var start = await f.StartLoginAsync(verifier);
    Check.Status(await f.CompleteAsync(start, ApiApplication.Secret()), 400);
    Check.Status(await f.CompleteAsync(start, verifier), 202);
    Check.Status(await f.CallbackAsync(start, "alice"), 200);
    Check.Status(await f.CallbackAsync(start, "alice"), 400);
    var attempts = await Task.WhenAll(f.CompleteAsync(start, verifier), f.CompleteAsync(start, verifier));
    Check.That(attempts.Count(x => (int)x.StatusCode == 200) == 1 && attempts.Count(x => (int)x.StatusCode == 400) == 1,
        "Concurrent completion issued more than one session");
    Check.Status(await f.CompleteAsync(start, verifier), 400);
});
await Test("account upsert, token hashing, per-device logout", async () =>
{
    await using var f = await Fixture.StartAsync();
    var first = await f.SignInAsync("alice");
    var second = await f.SignInAsync("alice");
    Check.That(first.UserId == second.UserId && first.Token != second.Token, "Account/session identity mismatch");
    Check.That(await f.Store.FindSessionAsync(first.Token, f.Clock.Now.UtcDateTime) is null, "Raw session token stored");
    Check.That(await f.Store.FindSessionAsync(ApiApplication.Hash(first.Token), f.Clock.Now.UtcDateTime) is not null, "Session hash missing");
    Check.Status(await f.SendAsync(HttpMethod.Get, "/me", first.Token), 200);
    Check.Status(await f.SendAsync(HttpMethod.Post, "/auth/logout", first.Token), 204);
    Check.Status(await f.SendAsync(HttpMethod.Get, "/me", first.Token), 401);
    Check.Status(await f.SendAsync(HttpMethod.Get, "/me", second.Token), 200);
});
await Test("expired logins and sessions rejected without waiting for Mongo TTL", async () =>
{
    await using var f = await Fixture.StartAsync();
    var session = await f.SignInAsync("alice");
    var verifier = ApiApplication.Secret();
    var pending = await f.StartLoginAsync(verifier);
    f.Clock.Now = f.Clock.Now.AddMinutes(6);
    Check.Status(await f.CallbackAsync(pending, "alice"), 400);
    Check.Status(await f.CompleteAsync(pending, verifier), 400);
    f.Clock.Now = f.Clock.Now.AddDays(31);
    Check.Status(await f.SendAsync(HttpMethod.Get, "/me", session.Token), 401);
});
await Test("invalid/cancelled Google identity cannot create a session", async () =>
{
    await using var f = await Fixture.StartAsync();
    Check.Status(await f.Http.GetAsync("/auth/google/callback?state=missing&code=alice"), 400);
    foreach (var code in new[] { "invalid", "" })
    {
        var verifier = ApiApplication.Secret();
        var start = await f.StartLoginAsync(verifier);
        Check.Status(await f.CallbackAsync(start, code), 400);
        Check.Status(await f.CompleteAsync(start, verifier), 400);
    }
    var cancelled = await f.StartLoginAsync(ApiApplication.Secret());
    var query = QueryHelpers.ParseQuery(new Uri(cancelled["authorizationUrl"]!.GetValue<string>()).Query);
    Check.Status(await f.Http.GetAsync($"/auth/google/callback?state={query["state"]}&error=access_denied"), 400);
    Check.Status(await f.CallbackAsync(cancelled, "alice"), 400);
});
await Test("account-owned backup creation, listing and restore", async () =>
{
    await using var f = await Fixture.StartAsync();
    var alice = await f.SignInAsync("alice"); var bob = await f.SignInAsync("bob");
    var snapshot = Snapshot();
    snapshot["userId"] = bob.UserId;
    var saved = await f.SendAsync(HttpMethod.Post, "/backups", alice.Token, snapshot);
    Check.Status(saved, 201);
    var info = (await saved.Content.ReadFromJsonAsync<JsonObject>())!;
    Check.That(info["projectCount"]!.GetValue<int>() == 1 && info["repositoryCount"]!.GetValue<int>() == 1, "Incorrect counts");
    var id = info["id"]!.GetValue<string>();
    Check.Status(await f.SendAsync(HttpMethod.Get, $"/backups/{id}", bob.Token), 404);
    var list = await f.SendAsync(HttpMethod.Get, "/backups", bob.Token);
    Check.That((await list.Content.ReadFromJsonAsync<JsonArray>())!.Count == 0, "Another account sees backups");
    var restore = await f.SendAsync(HttpMethod.Get, $"/backups/{id}", alice.Token);
    Check.Status(restore, 200);
    Check.That(JsonNode.DeepEquals(await restore.Content.ReadFromJsonAsync<JsonObject>(), BackupValidator.Parse(snapshot)), "Backup roundtrip failed");
    Check.Status(await f.SendAsync(HttpMethod.Post, "/backups", alice.Token, Snapshot()), 201);
    var own = await f.SendAsync(HttpMethod.Get, "/backups", alice.Token);
    Check.That((await own.Content.ReadFromJsonAsync<JsonArray>())!.Count == 2, "Backups overwritten");
});
await Test("legacy v1 backups migrate to desktop schema v2", async () =>
{
    await using var f = await Fixture.StartAsync();
    var session = await f.SignInAsync("alice");
    var saved = await f.SendAsync(HttpMethod.Post, "/backups", session.Token, Snapshot(1));
    Check.Status(saved, 201);
    var info = (await saved.Content.ReadFromJsonAsync<JsonObject>())!;
    var restore = await f.SendAsync(HttpMethod.Get, $"/backups/{info["id"]!.GetValue<string>()}", session.Token);
    var json = (await restore.Content.ReadFromJsonAsync<JsonObject>())!;
    Check.That(json["schemaVersion"]!.GetValue<int>() == 2 && json["set"]!["repositories"]!.AsArray().Count == 1,
        "Legacy migration failed");
});
await Test("invalid backup schema, IDs, ownership, commands and limits rejected", async () =>
{
    await using var f = await Fixture.StartAsync();
    var session = await f.SignInAsync("alice");
    var mutations = new Action<JsonObject>[] {
        s => s["schemaVersion"] = 99,
        s => s["set"]!["repositories"]![0]!["groupId"] = Guid.NewGuid().ToString(),
        s => s["set"]!["repositories"]![0]!["url"] = "file:///etc/passwd",
        s => s["set"]!["repositories"]![0]!["command"] = new string('x', 8001),
        s => s["set"]!["repositories"]!.AsArray().Add(s["set"]!["repositories"]![0]!.DeepClone()),
        s => s["appearance"]!["theme"] = "invalid",
        s => s["set"]!["name"] = "   "
    };
    foreach (var mutate in mutations)
    {
        var invalid = Snapshot(); mutate(invalid);
        Check.Status(await f.SendAsync(HttpMethod.Post, "/backups", session.Token, invalid), 400);
    }
    var huge = Snapshot(); huge["padding"] = new string('x', BackupValidator.MaxBytes);
    Check.Status(await f.SendAsync(HttpMethod.Post, "/backups", session.Token, huge), 413);
});
await Test("request validation and login rate limiting", async () =>
{
    await using var f = await Fixture.StartAsync();
    Check.Status(await f.Http.PostAsJsonAsync("/auth/desktop/start", new { challenge = "bad" }), 400);
    for (var i = 0; i < 9; i++) await f.StartLoginAsync(ApiApplication.Secret());
    var response = await f.Http.PostAsJsonAsync("/auth/desktop/start", new
        { challenge = ApiApplication.Secret(), deviceId = Guid.NewGuid().ToString(), deviceName = "Laptop" });
    Check.Status(response, 429);
});
await Test("production Google URL includes exact redirect, state, nonce and S256", () =>
{
    var options = new ApiOptions("https://api.example.com", "mongodb://localhost", "db", "client", "secret");
    using var http = new HttpClient();
    var url = new Uri(new GoogleProvider(http, options).AuthorizationUrl("state", "nonce", "challenge"));
    var query = QueryHelpers.ParseQuery(url.Query);
    Check.That(url.Host == "accounts.google.com" && query["redirect_uri"] == "https://api.example.com/auth/google/callback" &&
        query["nonce"] == "nonce" && query["state"] == "state" && query["code_challenge_method"] == "S256" &&
        query["code_challenge"] == "challenge", "Incorrect Google URL");
    return Task.CompletedTask;
});
await Test("insecure production origin rejected", () =>
{
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["PUBLIC_API_ORIGIN"] = "http://api.example.com", ["MONGODB_URI"] = "mongodb://localhost", ["GOOGLE_CLIENT_ID"] = "id", ["GOOGLE_CLIENT_SECRET"] = "secret" }).Build();
    try { ApiOptions.Read(config); throw new Exception("Insecure origin accepted"); }
    catch (InvalidOperationException) { }
    return Task.CompletedTask;
});
if (Environment.GetEnvironmentVariable("TEST_MONGODB_URI") is string mongoUri)
{
    await Test("existing Node BSON users, sessions, backups and indexes remain usable", async () =>
    {
        await using var f = await Fixture.StartAsync();
        var db = new MongoClient(mongoUri).GetDatabase(f.Options.Database);
        var userId = Guid.NewGuid().ToString(); var token = ApiApplication.Secret();
        await db.GetCollection<BsonDocument>("users").InsertOneAsync(new BsonDocument
        {
            ["_id"] = userId, ["id"] = Guid.NewGuid().ToString(), ["googleSub"] = "legacy",
            ["email"] = "legacy@gmail.com", ["name"] = "Legacy user"
        });
        await db.GetCollection<BsonDocument>("sessions").InsertOneAsync(new BsonDocument
        {
            ["_id"] = Guid.NewGuid().ToString(), ["userId"] = userId, ["deviceId"] = Guid.NewGuid().ToString(),
            ["deviceName"] = "Existing desktop", ["tokenHash"] = ApiApplication.Hash(token),
            ["expiresAt"] = DateTime.UtcNow.AddDays(1)
        });
        var backupId = Guid.NewGuid().ToString();
        await db.GetCollection<BsonDocument>("project_backups").InsertOneAsync(new BsonDocument
        {
            ["_id"] = backupId, ["userId"] = userId, ["createdAt"] = DateTime.UtcNow,
            ["snapshot"] = BsonDocument.Parse(Snapshot(1).ToJsonString())
        });
        await f.Store.InitializeAsync();
        var me = await f.SendAsync(HttpMethod.Get, "/me", token);
        Check.Status(me, 200);
        Check.That((await me.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>() == userId,
            "Existing account ID changed");
        var restored = await f.SendAsync(HttpMethod.Get, $"/backups/{backupId}", token);
        Check.Status(restored, 200);
        Check.That((await restored.Content.ReadFromJsonAsync<JsonObject>())!["schemaVersion"]!.GetValue<int>() == 2,
            "Existing legacy backup not migrated");
        var login = await f.SignInAsync("legacy");
        Check.That(login.UserId == userId, "Google sign-in duplicated existing account");
    });
}
Console.WriteLine($"Integration checks finished. Failures: {failures}. Store: {(Environment.GetEnvironmentVariable("TEST_MONGODB_URI") is null ? "test memory store" : "real MongoDB")}. Google identity is stubbed only in this test executable.");
Environment.ExitCode = failures == 0 ? 0 : 1;
