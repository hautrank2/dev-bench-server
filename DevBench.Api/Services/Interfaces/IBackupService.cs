using System.Text.Json.Nodes;
using DevBench.Api.Models.Dtos;
using DevBench.Api.Models;

namespace DevBench.Api.Services.Interfaces;

public interface IBackupService
{
    Task<IReadOnlyList<BackupInfo>> ListAsync(SessionRecord session);
    Task<BackupInfo> CreateAsync(SessionRecord session, JsonObject raw);
    Task<JsonObject> RestoreAsync(SessionRecord session, string id);
}
