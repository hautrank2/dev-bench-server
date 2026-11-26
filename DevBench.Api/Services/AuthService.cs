using System.Text.RegularExpressions;
using DevBench.Api.Models.Dtos;
using DevBench.Api.Models;
using DevBench.Api.Repositories;
using DevBench.Api.Security;
using DevBench.Api.Services.Interfaces;

namespace DevBench.Api.Services;

public sealed partial class AuthService(ICloudStore store, IGoogleProvider google, TimeProvider clock, ISessionService sessions) : IAuthService
{
    [GeneratedRegex("^[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant)] private static partial Regex ChallengePattern();
    [GeneratedRegex("^[A-Za-z0-9_-]{43,128}$", RegexOptions.CultureInvariant)] private static partial Regex VerifierPattern();

    public async Task<LoginStartResponse> StartAsync(LoginStart input)
    {
        if (input.Challenge is null || !ChallengePattern().IsMatch(input.Challenge) ||
            !Guid.TryParseExact(input.DeviceId, "D", out _) || string.IsNullOrWhiteSpace(input.DeviceName) || input.DeviceName.Trim().Length > 100)
            throw new ApiException(400, "Invalid request");
        var login = new LoginRecord { State = TokenSecurity.Secret(), Nonce = TokenSecurity.Secret(),
            GoogleVerifier = TokenSecurity.Secret(), Challenge = input.Challenge, DeviceId = input.DeviceId!,
            DeviceName = input.DeviceName.Trim(), ExpiresAt = clock.GetUtcNow().UtcDateTime.AddMinutes(5) };
        await store.CreateLoginAsync(login);
        return new(login.Id, google.AuthorizationUrl(login.State, login.Nonce, TokenSecurity.Hash(login.GoogleVerifier)));
    }

    public async Task<GoogleCallbackResult> CallbackAsync(string state, string code, bool cancelled, CancellationToken cancellationToken)
    {
        const string invalid = "This sign-in link is invalid or expired. Return to Dev Bench and try again.";
        if (string.IsNullOrEmpty(state) || state.Length > 256) return new(false, invalid);
        var login = await store.ConsumeStateAsync(state, clock.GetUtcNow().UtcDateTime);
        if (login is null) return new(false, invalid);
        if (cancelled || string.IsNullOrEmpty(code))
        {
            await store.DeleteLoginAsync(login.Id);
            return new(false, "Sign-in cancelled. Return to Dev Bench.");
        }
        try
        {
            var identity = await google.ExchangeAsync(code, login.GoogleVerifier, login.Nonce, cancellationToken);
            var user = await store.UpsertUserAsync(identity);
            await store.SetLoginUserAsync(login.Id, user.Id);
            return new(true, "You are signed in. You can close this tab and return to Dev Bench.");
        }
        catch
        {
            await store.DeleteLoginAsync(login.Id);
            return new(false, "Google sign-in could not be verified. Return to Dev Bench and try again.");
        }
    }

    public async Task<LoginCompleteResponse?> CompleteAsync(LoginComplete input)
    {
        if (!Guid.TryParseExact(input.LoginId, "D", out _) || input.Verifier is null || !VerifierPattern().IsMatch(input.Verifier))
            throw new ApiException(400, "Invalid request");
        var now = clock.GetUtcNow().UtcDateTime;
        var challenge = TokenSecurity.Hash(input.Verifier);
        var login = await store.FindLoginAsync(input.LoginId!, challenge, now);
        if (login is null) throw new ApiException(400, "Sign-in expired or cancelled. Try again.");
        if (login.UserId is null) return null;
        var consumed = await store.ConsumeLoginAsync(login.Id, challenge, now);
        if (consumed?.UserId is null) throw new ApiException(400, "Sign-in was already completed.");
        var user = await store.FindUserAsync(consumed.UserId) ?? throw new ApiException(401, "Account not found");
        var token = TokenSecurity.Secret();
        await store.CreateSessionAsync(new() { UserId = user.Id, DeviceId = consumed.DeviceId, DeviceName = consumed.DeviceName,
            TokenHash = TokenSecurity.Hash(token), ExpiresAt = now.AddDays(30) });
        return new(token, user.Account());
    }

    public async Task<Account> MeAsync(string authorization)
    {
        var session = await sessions.RequireAsync(authorization, "Session expired. Sign in again.");
        var user = await store.FindUserAsync(session.UserId) ?? throw new ApiException(401, "Account not found");
        return user.Account();
    }

    public async Task LogoutAsync(string authorization)
    {
        var session = await sessions.FindAsync(authorization);
        if (session is not null) await store.DeleteSessionAsync(session.Id);
    }
}
