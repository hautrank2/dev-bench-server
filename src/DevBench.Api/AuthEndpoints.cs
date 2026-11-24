using System.Text.RegularExpressions;

namespace DevBench.Api;

public static partial class AuthEndpoints
{
    [GeneratedRegex("^[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant)] private static partial Regex ChallengePattern();
    [GeneratedRegex("^[A-Za-z0-9_-]{43,128}$", RegexOptions.CultureInvariant)] private static partial Regex VerifierPattern();

    public static void Map(WebApplication app)
    {
        app.MapPost("/auth/desktop/start", async (LoginStart input, ICloudStore store, IGoogleProvider google, TimeProvider clock) =>
        {
            if (input.Challenge is null || !ChallengePattern().IsMatch(input.Challenge) ||
                !Guid.TryParseExact(input.DeviceId, "D", out _) || string.IsNullOrWhiteSpace(input.DeviceName) || input.DeviceName.Trim().Length > 100)
                return ApiApplication.Error(400, "Invalid request");
            var login = new LoginRecord { State = ApiApplication.Secret(), Nonce = ApiApplication.Secret(),
                GoogleVerifier = ApiApplication.Secret(), Challenge = input.Challenge, DeviceId = input.DeviceId!,
                DeviceName = input.DeviceName.Trim(), ExpiresAt = clock.GetUtcNow().UtcDateTime.AddMinutes(5) };
            await store.CreateLoginAsync(login);
            return Results.Ok(new { loginId = login.Id, authorizationUrl = google.AuthorizationUrl(login.State, login.Nonce,
                ApiApplication.Hash(login.GoogleVerifier)) });
        }).RequireRateLimiting("login");

        app.MapGet("/auth/google/callback", async (HttpContext context, ICloudStore store, IGoogleProvider google, TimeProvider clock) =>
        {
            var query = context.Request.Query;
            var state = query["state"].ToString();
            if (string.IsNullOrEmpty(state) || state.Length > 256)
                return Results.Text("This sign-in link is invalid or expired. Return to Dev Bench and try again.", statusCode: 400);
            var login = await store.ConsumeStateAsync(state, clock.GetUtcNow().UtcDateTime);
            if (login is null)
                return Results.Text("This sign-in link is invalid or expired. Return to Dev Bench and try again.", statusCode: 400);
            var code = query["code"].ToString();
            if (query.ContainsKey("error") || string.IsNullOrEmpty(code))
            {
                await store.DeleteLoginAsync(login.Id);
                return Results.Text("Sign-in cancelled. Return to Dev Bench.", statusCode: 400);
            }
            try
            {
                var identity = await google.ExchangeAsync(code, login.GoogleVerifier, login.Nonce, context.RequestAborted);
                var user = await store.UpsertUserAsync(identity);
                await store.SetLoginUserAsync(login.Id, user.Id);
                return Results.Text("You are signed in. You can close this tab and return to Dev Bench.");
            }
            catch
            {
                await store.DeleteLoginAsync(login.Id);
                return Results.Text("Google sign-in could not be verified. Return to Dev Bench and try again.", statusCode: 400);
            }
        });

        app.MapPost("/auth/desktop/complete", async (LoginComplete input, ICloudStore store, TimeProvider clock) =>
        {
            if (!Guid.TryParseExact(input.LoginId, "D", out _) || input.Verifier is null || !VerifierPattern().IsMatch(input.Verifier))
                return ApiApplication.Error(400, "Invalid request");
            var now = clock.GetUtcNow().UtcDateTime;
            var challenge = ApiApplication.Hash(input.Verifier);
            var login = await store.FindLoginAsync(input.LoginId!, challenge, now);
            if (login is null) return ApiApplication.Error(400, "Sign-in expired or cancelled. Try again.");
            if (login.UserId is null) return Results.Json(new { pending = true }, statusCode: 202);
            var consumed = await store.ConsumeLoginAsync(login.Id, challenge, now);
            if (consumed?.UserId is null) return ApiApplication.Error(400, "Sign-in was already completed.");
            var user = await store.FindUserAsync(consumed.UserId);
            if (user is null) return ApiApplication.Error(401, "Account not found");
            var token = ApiApplication.Secret();
            await store.CreateSessionAsync(new() { UserId = user.Id, DeviceId = consumed.DeviceId, DeviceName = consumed.DeviceName,
                TokenHash = ApiApplication.Hash(token), ExpiresAt = now.AddDays(30) });
            return Results.Ok(new { token, account = user.Account() });
        });

        app.MapGet("/me", async (HttpContext context, ICloudStore store, TimeProvider clock) =>
        {
            var session = await ApiApplication.SessionAsync(context, store, clock);
            if (session is null) return ApiApplication.Error(401, "Session expired. Sign in again.");
            var user = await store.FindUserAsync(session.UserId);
            return user is null ? ApiApplication.Error(401, "Account not found") : Results.Ok(user.Account());
        });
        app.MapPost("/auth/logout", async (HttpContext context, ICloudStore store, TimeProvider clock) =>
        {
            var session = await ApiApplication.SessionAsync(context, store, clock);
            if (session is not null) await store.DeleteSessionAsync(session.Id);
            return Results.NoContent();
        });
    }
}
