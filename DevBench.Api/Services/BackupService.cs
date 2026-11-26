using System.Text.Json;
using System.Text.Json.Nodes;
using DevBench.Api.Models.Dtos;
using DevBench.Api.Models;
using DevBench.Api.Repositories;
using DevBench.Api.Services.Interfaces;
using DevBench.Api.Validation;
using MongoDB.Bson;

namespace DevBench.Api.Services;

public sealed class BackupService(ICloudStore store, TimeProvider clock, ILogger<BackupService> logger) : IBackupService
{
    public async Task<IReadOnlyList<BackupInfo>> ListAsync(SessionRecord session)
    {
        var items = new List<BackupInfo>();
        foreach (var backup in await store.ListBackupsAsync(session.UserId))
        {
            // One unreadable snapshot must not hide the account's other backups.
            try { items.Add(backup.Info()); }
            catch (Exception exception) when (exception is ArgumentException or JsonException)
            {
                logger.LogWarning("Skipped unreadable backup {BackupId}", backup.Id);
            }
        }
        return items;
    }

    public async Task<BackupInfo> CreateAsync(SessionRecord session, JsonObject raw)
    {
        var snapshot = BackupValidator.Parse(raw);
        var backup = new BackupRecord { UserId = session.UserId, CreatedAt = clock.GetUtcNow().UtcDateTime,
            Snapshot = BsonDocument.Parse(snapshot.ToJsonString()) };
        await store.CreateBackupAsync(backup);
        return backup.Info();
    }

    public async Task<JsonObject> RestoreAsync(SessionRecord session, string id)
    {
        if (!Guid.TryParseExact(id, "D", out _)) throw new ApiException(400, "Invalid request");
        var backup = await store.FindBackupAsync(id, session.UserId) ?? throw new ApiException(404, "Backup not found");
        return BackupValidator.Parse(backup.Json());
    }
}
