using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace DevBench.Api;

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
        Require(Encoding.UTF8.GetByteCount(input.ToJsonString()) <= MaxBytes);
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
            repositories.Add(new JsonObject { ["id"] = id, ["groupId"] = groupId, ["name"] = name,
                ["folder"] = Text(repo, "folder", 1, int.MaxValue, true), ["command"] = Text(repo, "command", 0, 8000, true),
                ["url"] = url, ["includeInRunAll"] = repo["includeInRunAll"]!.GetValue<bool>() });
        }
        var normalized = new JsonObject
        {
            ["kind"] = "dev-bench-project-set", ["schemaVersion"] = 2, ["exportedAt"] = exportedAt,
            ["sourceDevice"] = Text(input, "sourceDevice", 1, 100, true), ["platform"] = platform,
            ["appearance"] = new JsonObject { ["theme"] = theme, ["terminalTheme"] = terminal },
            ["set"] = new JsonObject { ["id"] = Uuid(rawSet, "id"), ["name"] = Text(rawSet, "name", 1, 100, true),
                ["projects"] = projects, ["repositories"] = repositories }
        };
        Require(Encoding.UTF8.GetByteCount(normalized.ToJsonString()) <= MaxBytes);
        return normalized;
    }

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
