using DevBench.Api.Models;
namespace DevBench.Api.Repositories;

public interface ICloudStore
{
    Task InitializeAsync();
    Task CreateLoginAsync(LoginRecord login);
    Task<LoginRecord?> ConsumeStateAsync(string state, DateTime now);
    Task DeleteLoginAsync(string id);
    Task SetLoginUserAsync(string id, string userId);
    Task<LoginRecord?> FindLoginAsync(string id, string challenge, DateTime now);
    Task<LoginRecord?> ConsumeLoginAsync(string id, string challenge, DateTime now);
    Task<UserRecord> UpsertUserAsync(GoogleIdentity identity);
    Task<UserRecord?> FindUserAsync(string id);
    Task CreateSessionAsync(SessionRecord session);
    Task<SessionRecord?> FindSessionAsync(string hash, DateTime now);
    Task DeleteSessionAsync(string id);
    Task CreateBackupAsync(BackupRecord backup);
    Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(string userId);
    Task<BackupRecord?> FindBackupAsync(string id, string userId);
}
