using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DevBench.Api;

public sealed record LoginStart(string? Challenge, string? DeviceId, string? DeviceName);
public sealed record LoginComplete(string? LoginId, string? Verifier);
public sealed record Account(string Id, string Email, string Name);
public sealed record GoogleIdentity(string Sub, string Email, string Name);
public sealed record BackupInfo(string Id, string Name, string SourceDevice, string Platform,
    int ProjectCount, int RepositoryCount, string CreatedAt);

[BsonIgnoreExtraElements]
public sealed class UserRecord
{
    [BsonId] public string Id { get; set; } = Guid.NewGuid().ToString();
    [BsonElement("googleSub")] public string GoogleSub { get; set; } = "";
    [BsonElement("email")] public string Email { get; set; } = "";
    [BsonElement("name")] public string Name { get; set; } = "";
    public Account Account() => new(Id, Email, Name);
}

[BsonIgnoreExtraElements]
public sealed class LoginRecord
{
    [BsonId] public string Id { get; set; } = Guid.NewGuid().ToString();
    [BsonElement("state"), BsonIgnoreIfNull] public string? State { get; set; }
    [BsonElement("nonce")] public string Nonce { get; set; } = "";
    [BsonElement("googleVerifier")] public string GoogleVerifier { get; set; } = "";
    [BsonElement("challenge")] public string Challenge { get; set; } = "";
    [BsonElement("deviceId")] public string DeviceId { get; set; } = "";
    [BsonElement("deviceName")] public string DeviceName { get; set; } = "";
    [BsonElement("expiresAt")] public DateTime ExpiresAt { get; set; }
    [BsonElement("userId"), BsonIgnoreIfNull] public string? UserId { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class SessionRecord
{
    [BsonId] public string Id { get; set; } = Guid.NewGuid().ToString();
    [BsonElement("userId")] public string UserId { get; set; } = "";
    [BsonElement("deviceId")] public string DeviceId { get; set; } = "";
    [BsonElement("deviceName")] public string DeviceName { get; set; } = "";
    [BsonElement("tokenHash")] public string TokenHash { get; set; } = "";
    [BsonElement("expiresAt")] public DateTime ExpiresAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class BackupRecord
{
    [BsonId] public string Id { get; set; } = Guid.NewGuid().ToString();
    [BsonElement("userId")] public string UserId { get; set; } = "";
    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("snapshot")] public BsonDocument Snapshot { get; set; } = new();
    public JsonObject Json() => JsonNode.Parse(Snapshot.ToJson(new MongoDB.Bson.IO.JsonWriterSettings
        { OutputMode = MongoDB.Bson.IO.JsonOutputMode.RelaxedExtendedJson }))!.AsObject();
    public BackupInfo Info()
    {
        // Normalize snapshots created by the former Node backend as well.
        var json = BackupValidator.Parse(Json());
        var set = json["set"]!.AsObject();
        return new(Id, set["name"]!.GetValue<string>(), json["sourceDevice"]!.GetValue<string>(),
            json["platform"]!.GetValue<string>(), set["projects"]!.AsArray().Count,
            set["repositories"]!.AsArray().Count, CreatedAt.ToUniversalTime().ToString("O"));
    }
}
