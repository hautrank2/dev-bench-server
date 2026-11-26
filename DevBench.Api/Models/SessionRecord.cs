using MongoDB.Bson.Serialization.Attributes;

namespace DevBench.Api.Models;

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
