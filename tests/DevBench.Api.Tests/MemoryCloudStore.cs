using DevBench.Api;

namespace DevBench.Api.Tests;

// Test double only: the production application always registers MongoCloudStore.
public sealed class MemoryCloudStore : ICloudStore
{
    private readonly object gate = new();
    public List<UserRecord> Users { get; } = [];
    public List<LoginRecord> Logins { get; } = [];
    public List<SessionRecord> Sessions { get; } = [];
    public List<BackupRecord> Backups { get; } = [];
    public Task InitializeAsync() => Task.CompletedTask;
    public Task CreateLoginAsync(LoginRecord login) { lock (gate) Logins.Add(login); return Task.CompletedTask; }
    public Task<LoginRecord?> ConsumeStateAsync(string state, DateTime now)
    {
        lock (gate)
        {
            var login = Logins.FirstOrDefault(x => x.State == state && x.ExpiresAt > now);
            if (login is not null) login.State = null;
            return Task.FromResult(login);
        }
    }
    public Task DeleteLoginAsync(string id) { lock (gate) Logins.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
    public Task SetLoginUserAsync(string id, string userId)
    {
        lock (gate) { var login = Logins.Find(x => x.Id == id); if (login is not null) login.UserId = userId; }
        return Task.CompletedTask;
    }
    public Task<LoginRecord?> FindLoginAsync(string id, string challenge, DateTime now)
    {
        lock (gate) return Task.FromResult(Logins.Find(x => x.Id == id && x.Challenge == challenge && x.ExpiresAt > now));
    }
    public Task<LoginRecord?> ConsumeLoginAsync(string id, string challenge, DateTime now)
    {
        lock (gate)
        {
            var login = Logins.Find(x => x.Id == id && x.Challenge == challenge && x.ExpiresAt > now && x.UserId != null);
            if (login is not null) Logins.Remove(login);
            return Task.FromResult(login);
        }
    }
    public Task<UserRecord> UpsertUserAsync(GoogleIdentity identity)
    {
        lock (gate)
        {
            var user = Users.Find(x => x.GoogleSub == identity.Sub);
            if (user is null) { user = new() { GoogleSub = identity.Sub }; Users.Add(user); }
            user.Email = identity.Email; user.Name = identity.Name;
            return Task.FromResult(user);
        }
    }
    public Task<UserRecord?> FindUserAsync(string id) { lock (gate) return Task.FromResult(Users.Find(x => x.Id == id)); }
    public Task CreateSessionAsync(SessionRecord session) { lock (gate) Sessions.Add(session); return Task.CompletedTask; }
    public Task<SessionRecord?> FindSessionAsync(string hash, DateTime now)
    { lock (gate) return Task.FromResult(Sessions.Find(x => x.TokenHash == hash && x.ExpiresAt > now)); }
    public Task DeleteSessionAsync(string id) { lock (gate) Sessions.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
    public Task CreateBackupAsync(BackupRecord backup) { lock (gate) Backups.Add(backup); return Task.CompletedTask; }
    public Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(string userId)
    { lock (gate) return Task.FromResult<IReadOnlyList<BackupRecord>>(Backups.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Take(500).ToArray()); }
    public Task<BackupRecord?> FindBackupAsync(string id, string userId)
    { lock (gate) return Task.FromResult(Backups.Find(x => x.Id == id && x.UserId == userId)); }
}
