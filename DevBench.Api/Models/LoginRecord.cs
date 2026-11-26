using MongoDB.Bson.Serialization.Attributes;

namespace DevBench.Api.Models;

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
