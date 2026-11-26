using System.Text.Json.Nodes;
using DevBench.Api.Models.Dtos;
using DevBench.Api.Validation;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace DevBench.Api.Models;

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
