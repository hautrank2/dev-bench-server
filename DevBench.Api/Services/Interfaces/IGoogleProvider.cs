using DevBench.Api.Models;

namespace DevBench.Api.Services.Interfaces;

public interface IGoogleProvider
{
    string AuthorizationUrl(string state, string nonce, string challenge);
    Task<GoogleIdentity> ExchangeAsync(string code, string verifier, string nonce, CancellationToken cancellationToken);
}
