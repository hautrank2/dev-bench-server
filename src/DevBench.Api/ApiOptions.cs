namespace DevBench.Api;

public sealed record ApiOptions(string PublicOrigin, string MongoUri, string Database,
    string GoogleClientId, string GoogleClientSecret)
{
    public string RedirectUri => new Uri(new Uri(PublicOrigin), "/auth/google/callback").ToString();

    public static ApiOptions Read(IConfiguration config)
    {
        string Required(string key) => !string.IsNullOrWhiteSpace(config[key])
            ? config[key]! : throw new InvalidOperationException($"Missing {key}. See README.md.");
        var origin = Required("PUBLIC_API_ORIGIN");
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            !(uri.Scheme == "https" || (uri.Scheme == "http" &&
                uri.Host is "localhost" or "127.0.0.1")) ||
            uri.AbsolutePath != "/" || uri.UserInfo != "" || uri.Query != "" || uri.Fragment != "")
            throw new InvalidOperationException("PUBLIC_API_ORIGIN must be an HTTPS origin (HTTP allowed on localhost).");
        return new(uri.GetLeftPart(UriPartial.Authority), Required("MONGODB_URI"),
            config["MONGODB_DB"] ?? "dev_bench", Required("GOOGLE_CLIENT_ID"), Required("GOOGLE_CLIENT_SECRET"));
    }
}
