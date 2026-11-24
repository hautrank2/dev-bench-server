using MongoDB.Driver;

namespace DevBench.Api;

public sealed class MongoCloudStore : ICloudStore
{
    private readonly IMongoCollection<UserRecord> users;
    private readonly IMongoCollection<LoginRecord> logins;
    private readonly IMongoCollection<SessionRecord> sessions;
    private readonly IMongoCollection<BackupRecord> backups;

    public MongoCloudStore(ApiOptions options)
    {
        var settings = MongoClientSettings.FromConnectionString(options.MongoUri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
        var db = new MongoClient(settings).GetDatabase(options.Database);
        users = db.GetCollection<UserRecord>("users");
        logins = db.GetCollection<LoginRecord>("desktop_logins");
        sessions = db.GetCollection<SessionRecord>("sessions");
        backups = db.GetCollection<BackupRecord>("project_backups");
    }

    public async Task InitializeAsync()
    {
        // Names match the existing Node indexes, allowing reuse of the same database.
        await users.Indexes.CreateOneAsync(new CreateIndexModel<UserRecord>(Builders<UserRecord>.IndexKeys.Ascending(x => x.GoogleSub),
            new CreateIndexOptions { Unique = true, Name = "googleSub_1" }));
        await sessions.Indexes.CreateManyAsync([
            new(Builders<SessionRecord>.IndexKeys.Ascending(x => x.TokenHash), new CreateIndexOptions { Unique = true, Name = "tokenHash_1" }),
            new(Builders<SessionRecord>.IndexKeys.Ascending(x => x.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expiresAt_1" })]);
        await logins.Indexes.CreateManyAsync([
            new(Builders<LoginRecord>.IndexKeys.Ascending(x => x.State), new CreateIndexOptions { Unique = true, Sparse = true, Name = "state_1" }),
            new(Builders<LoginRecord>.IndexKeys.Ascending(x => x.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expiresAt_1" })]);
        await backups.Indexes.CreateOneAsync(new CreateIndexModel<BackupRecord>(Builders<BackupRecord>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.CreatedAt),
            new CreateIndexOptions { Name = "userId_1_createdAt_-1" }));
    }

    public Task CreateLoginAsync(LoginRecord login) => logins.InsertOneAsync(login);
    public Task<LoginRecord?> ConsumeStateAsync(string state, DateTime now) => logins.FindOneAndUpdateAsync<LoginRecord>(
        x => x.State == state && x.ExpiresAt > now, Builders<LoginRecord>.Update.Unset(x => x.State),
        new() { ReturnDocument = ReturnDocument.Before })!;
    public Task DeleteLoginAsync(string id) => logins.DeleteOneAsync(x => x.Id == id);
    public Task SetLoginUserAsync(string id, string userId) => logins.UpdateOneAsync(x => x.Id == id,
        Builders<LoginRecord>.Update.Set(x => x.UserId, userId));
    public async Task<LoginRecord?> FindLoginAsync(string id, string challenge, DateTime now) =>
        await logins.Find(x => x.Id == id && x.Challenge == challenge && x.ExpiresAt > now).FirstOrDefaultAsync();
    public Task<LoginRecord?> ConsumeLoginAsync(string id, string challenge, DateTime now) =>
        logins.FindOneAndDeleteAsync(x => x.Id == id && x.Challenge == challenge && x.ExpiresAt > now && x.UserId != null)!;

    public async Task<UserRecord> UpsertUserAsync(GoogleIdentity identity)
    {
        var update = Builders<UserRecord>.Update.Set(x => x.Email, identity.Email).Set(x => x.Name, identity.Name)
            .SetOnInsert(x => x.Id, Guid.NewGuid().ToString()).SetOnInsert(x => x.GoogleSub, identity.Sub);
        try
        {
            return await users.FindOneAndUpdateAsync(x => x.GoogleSub == identity.Sub, update,
                new() { IsUpsert = true, ReturnDocument = ReturnDocument.After });
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            // Another device may create the same account concurrently.
            return await users.Find(x => x.GoogleSub == identity.Sub).FirstAsync();
        }
    }
    public async Task<UserRecord?> FindUserAsync(string id) => await users.Find(x => x.Id == id).FirstOrDefaultAsync();
    public Task CreateSessionAsync(SessionRecord session) => sessions.InsertOneAsync(session);
    public async Task<SessionRecord?> FindSessionAsync(string hash, DateTime now) =>
        await sessions.Find(x => x.TokenHash == hash && x.ExpiresAt > now).FirstOrDefaultAsync();
    public Task DeleteSessionAsync(string id) => sessions.DeleteOneAsync(x => x.Id == id);
    public Task CreateBackupAsync(BackupRecord backup) => backups.InsertOneAsync(backup);
    public async Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(string userId) =>
        await backups.Find(x => x.UserId == userId).SortByDescending(x => x.CreatedAt).Limit(500).ToListAsync();
    public async Task<BackupRecord?> FindBackupAsync(string id, string userId) =>
        await backups.Find(x => x.Id == id && x.UserId == userId).FirstOrDefaultAsync();
}
