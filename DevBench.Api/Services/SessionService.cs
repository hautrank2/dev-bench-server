using DevBench.Api.Models;
using DevBench.Api.Repositories;
using DevBench.Api.Security;
using DevBench.Api.Services.Interfaces;

namespace DevBench.Api.Services;

public sealed class SessionService(ICloudStore store, TimeProvider clock) : ISessionService
{
    public async Task<SessionRecord?> FindAsync(string authorization)
    {
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
        var token = authorization[7..];
        return token.Length is 0 or > 256 ? null :
            await store.FindSessionAsync(TokenSecurity.Hash(token), clock.GetUtcNow().UtcDateTime);
    }

    public async Task<SessionRecord> RequireAsync(string authorization, string message) =>
        await FindAsync(authorization) ?? throw new ApiException(401, message);
}
