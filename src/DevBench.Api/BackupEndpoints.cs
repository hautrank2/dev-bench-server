using System.Text.Json.Nodes;
using MongoDB.Bson;

namespace DevBench.Api;

public static class BackupEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/backups", async (HttpContext context, ICloudStore store, TimeProvider clock) =>
        {
            var session = await ApiApplication.SessionAsync(context, store, clock);
            if (session is null) return ApiApplication.Error(401, "Sign in to view your backups");
            return Results.Ok((await store.ListBackupsAsync(session.UserId)).Select(backup => backup.Info()));
        });
        app.MapPost("/backups", async (HttpContext context, ICloudStore store, TimeProvider clock) =>
        {
            var session = await ApiApplication.SessionAsync(context, store, clock);
            if (session is null) return ApiApplication.Error(401, "Sign in to back up this set");
            var raw = await context.Request.ReadFromJsonAsync<JsonObject>(context.RequestAborted) ?? throw new ArgumentException("Missing backup");
            var snapshot = BackupValidator.Parse(raw);
            var backup = new BackupRecord { UserId = session.UserId, CreatedAt = clock.GetUtcNow().UtcDateTime,
                Snapshot = BsonDocument.Parse(snapshot.ToJsonString()) };
            await store.CreateBackupAsync(backup);
            return Results.Json(backup.Info(), statusCode: 201);
        });
        app.MapGet("/backups/{id}", async (string id, HttpContext context, ICloudStore store, TimeProvider clock) =>
        {
            var session = await ApiApplication.SessionAsync(context, store, clock);
            if (session is null) return ApiApplication.Error(401, "Sign in to restore a backup");
            if (!Guid.TryParseExact(id, "D", out _)) return ApiApplication.Error(400, "Invalid request");
            var backup = await store.FindBackupAsync(id, session.UserId);
            return backup is null ? ApiApplication.Error(404, "Backup not found") : Results.Json(BackupValidator.Parse(backup.Json()));
        });
    }
}
