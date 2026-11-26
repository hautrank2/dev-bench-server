using DevBench.Api.Models.Dtos;
using MongoDB.Bson.Serialization.Attributes;

namespace DevBench.Api.Models;

[BsonIgnoreExtraElements]
public sealed class UserRecord
{
    [BsonId] public string Id { get; set; } = Guid.NewGuid().ToString();
    [BsonElement("googleSub")] public string GoogleSub { get; set; } = "";
    [BsonElement("email")] public string Email { get; set; } = "";
    [BsonElement("name")] public string Name { get; set; } = "";
    public Account Account() => new(Id, Email, Name);
}
