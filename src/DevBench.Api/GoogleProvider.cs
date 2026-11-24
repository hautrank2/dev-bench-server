using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.AspNetCore.WebUtilities;

namespace DevBench.Api;

public interface IGoogleProvider
{
    string AuthorizationUrl(string state, string nonce, string challenge);
    Task<GoogleIdentity> ExchangeAsync(string code, string verifier, string nonce, CancellationToken cancellationToken);
}

public sealed class GoogleProvider(HttpClient http, ApiOptions options) : IGoogleProvider
{
    public string AuthorizationUrl(string state, string nonce, string challenge) => QueryHelpers.AddQueryString(
        "https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?>
        {
            ["client_id"] = options.GoogleClientId, ["redirect_uri"] = options.RedirectUri,
            ["response_type"] = "code", ["scope"] = "openid email profile", ["state"] = state,
            ["nonce"] = nonce, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
            ["prompt"] = "select_account"
        });

    public async Task<GoogleIdentity> ExchangeAsync(string code, string verifier, string nonce, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["code"] = code, ["code_verifier"] = verifier, ["client_id"] = options.GoogleClientId,
                ["client_secret"] = options.GoogleClientSecret, ["redirect_uri"] = options.RedirectUri,
                ["grant_type"] = "authorization_code"
            }), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var token = json.RootElement.GetProperty("id_token").GetString() ?? throw new InvalidOperationException("Missing ID token");
        var payload = await GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings
            { Audience = [options.GoogleClientId] });
        // Parse the nonce only after the library has verified the token signature, issuer, audience and expiry.
        using var claims = JsonDocument.Parse(WebEncoders.Base64UrlDecode(token.Split('.')[1]));
        if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email) ||
            !payload.EmailVerified || !claims.RootElement.TryGetProperty("nonce", out var tokenNonce) || tokenNonce.GetString() != nonce)
            throw new InvalidOperationException("Google identity verification failed");
        return new(payload.Subject, payload.Email, payload.Name ?? payload.Email);
    }
}
