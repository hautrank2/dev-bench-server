namespace DevBench.Api.Configuration;

public sealed record ApiOptions(string PublicOrigin, string MongoUri, string Database,
    string GoogleClientId, string GoogleClientSecret)
{
    public string RedirectUri => new Uri(new Uri(PublicOrigin), "/auth/google/callback").ToString();

    public static ApiOptions Read(IConfiguration config)
    {
        // Keep the original deployment variables working while supporting standard JSON sections.
        string? Value(string key, string legacyKey) => config[legacyKey] ?? config[key];
        string Required(string key, string legacyKey) => !string.IsNullOrWhiteSpace(Value(key, legacyKey))
            ? Value(key, legacyKey)! : throw new InvalidOperationException($"Missing {key} ({legacyKey}). See docs/configuration.md.");
        var origin = Required("Api:PublicOrigin", "PUBLIC_API_ORIGIN");
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            !(uri.Scheme == "https" || (uri.Scheme == "http" &&
                uri.Host is "localhost" or "127.0.0.1")) ||
            uri.AbsolutePath != "/" || uri.UserInfo != "" || uri.Query != "" || uri.Fragment != "")
            throw new InvalidOperationException("PUBLIC_API_ORIGIN must be an HTTPS origin (HTTP allowed on localhost).");
        return new(uri.GetLeftPart(UriPartial.Authority), Required("MongoDb:ConnectionString", "MONGODB_URI"),
            Value("MongoDb:DatabaseName", "MONGODB_DB") ?? "dev_bench",
            Required("Google:ClientId", "GOOGLE_CLIENT_ID"), Required("Google:ClientSecret", "GOOGLE_CLIENT_SECRET"));
    }
}
