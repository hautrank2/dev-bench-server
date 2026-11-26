using DevBench.Api.Models.Dtos;
using DevBench.Api.Models;

namespace DevBench.Api.Services.Interfaces;

public interface IAuthService
{
    Task<LoginStartResponse> StartAsync(LoginStart input);
    Task<GoogleCallbackResult> CallbackAsync(string state, string code, bool cancelled, CancellationToken cancellationToken);
    Task<LoginCompleteResponse?> CompleteAsync(LoginComplete input);
    Task<Account> MeAsync(string authorization);
    Task LogoutAsync(string authorization);
}
