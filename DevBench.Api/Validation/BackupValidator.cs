using System.Globalization;
using System.Text.Json.Nodes;
using System.Text;

namespace DevBench.Api.Validation;

public static class BackupValidator
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public static JsonObject Parse(JsonObject input)
    {
        try { return Normalize(input); }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            throw new ArgumentException("Invalid backup configuration", exception);
        }
    }

    private static JsonObject Normalize(JsonObject input)
    {
        Require(JsonSize(input) <= MaxBytes);
        Require(Text(input, "kind") == "dev-bench-project-set");
        var version = input["schemaVersion"]?.GetValue<int>();
        Require(version is 1 or 2);
        var exportedAt = Text(input, "exportedAt");
        Require(exportedAt.EndsWith('Z') && DateTimeOffset.TryParse(exportedAt, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out _));
        var platform = Text(input, "platform");
        Require(platform is "win32" or "darwin" or "linux" or "other");
        var appearance = Object(input, "appearance");
        var theme = appearance["theme"] is null ? "system" : Text(appearance, "theme");
        var terminal = appearance["terminalTheme"] is null ? "app" : Text(appearance, "terminalTheme");
        Require(theme is "system" or "light" or "dark");
        Require(terminal is "app" or "midnight" or "paper" or "rose");
        var rawSet = Object(input, "set");
        var projects = new JsonArray();
        var repositories = new JsonArray();
        var groupIds = new HashSet<string>();
        var repoIds = new HashSet<string>();
        if (version == 2)
        {
            foreach (var node in Array(rawSet, "projects"))
            {
                var project = node?.AsObject() ?? throw Invalid();
                var id = Uuid(project, "id");
                Require(groupIds.Add(id));
                projects.Add(new JsonObject { ["id"] = id, ["name"] = Text(project, "name", 1, 100, true),
                    ["description"] = project["description"] is null ? "" : Text(project, "description", 0, 500, true) });
            }
        }
        foreach (var node in Array(rawSet, version == 1 ? "projects" : "repositories"))
        {
            var repo = node?.AsObject() ?? throw Invalid();
            var id = Uuid(repo, "id");
            Require(repoIds.Add(id));
            var groupId = version == 1 && repo["groupId"] is null ? id : Uuid(repo, "groupId");
            var name = Text(repo, "name", 1, 120, true);
            if (version == 1 && groupIds.Add(groupId))
            {
                Require(name.Length <= 100);
                projects.Add(new JsonObject { ["id"] = groupId, ["name"] = name, ["description"] = "" });
            }
            Require(groupIds.Contains(groupId));
            var url = Text(repo, "url", 0, int.MaxValue, true);
            Require(url == "" || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"));
            Require(repo["includeInRunAll"] is JsonValue flag && flag.TryGetValue<bool>(out _));
            var repository = new JsonObject { ["id"] = id, ["groupId"] = groupId, ["name"] = name,
                ["folder"] = Text(repo, "folder", 1, int.MaxValue, true), ["command"] = Text(repo, "command", 0, 8000, true),
                ["url"] = url, ["includeInRunAll"] = repo["includeInRunAll"]!.GetValue<bool>() };
            if (repo.ContainsKey("workingDirectory"))
                repository["workingDirectory"] = Text(repo, "workingDirectory", 0, 4096, true);
            repositories.Add(repository);
        }
        var normalized = new JsonObject
        {
            ["kind"] = "dev-bench-project-set", ["schemaVersion"] = 2, ["exportedAt"] = exportedAt,
            ["sourceDevice"] = Text(input, "sourceDevice", 1, 100, true), ["platform"] = platform,
            ["appearance"] = new JsonObject { ["theme"] = theme, ["terminalTheme"] = terminal },
            ["set"] = new JsonObject { ["id"] = Uuid(rawSet, "id"), ["name"] = Text(rawSet, "name", 1, 100, true),
                ["projects"] = projects, ["repositories"] = repositories }
        };
        Require(JsonSize(normalized) <= MaxBytes);
        return normalized;
    }

    // UTF-8 size as the desktop measures it with JSON.stringify. ToJsonString() also escapes
    // characters such as '&' and non-ASCII text, which would reject backups within the limit.
    private static long JsonSize(JsonNode? node) => node switch
    {
        null => "null".Length,
        JsonObject obj => 2 + Math.Max(obj.Count - 1, 0) + obj.Sum(pair => StringSize(pair.Key) + 1 + JsonSize(pair.Value)),
        JsonArray array => 2 + Math.Max(array.Count - 1, 0) + array.Sum(item => JsonSize(item)),
        JsonValue value when value.TryGetValue<string>(out var text) => StringSize(text),
        _ => node.ToJsonString().Length
    };

    // Quotes, UTF-8 content, then the extra bytes of the only escapes JSON.stringify emits.
    private static long StringSize(string text) => 2 + Encoding.UTF8.GetByteCount(text) + text.Sum(character =>
        character is '"' or '\\' or '\b' or '\t' or '\n' or '\f' or '\r' ? 1L : character < ' ' ? 5L : 0L);

    private static JsonArray Array(JsonObject obj, string key)
    {
        var value = obj[key]?.AsArray() ?? throw Invalid();
        Require(value.Count <= 1000);
        return value;
    }
    private static JsonObject Object(JsonObject obj, string key) => obj[key]?.AsObject() ?? throw Invalid();
    private static string Text(JsonObject obj, string key, int min = 0, int max = int.MaxValue, bool trim = false)
    {
        var text = obj[key]?.GetValue<string>() ?? throw Invalid();
        if (trim) text = text.Trim();
        Require(text.Length >= min && text.Length <= max);
        return text;
    }
    private static string Uuid(JsonObject obj, string key)
    {
        var id = Text(obj, key);
        Require(Guid.TryParseExact(id, "D", out _));
        return id;
    }
    private static void Require(bool condition) { if (!condition) throw Invalid(); }
    private static ArgumentException Invalid() => new("Invalid backup configuration");
}
