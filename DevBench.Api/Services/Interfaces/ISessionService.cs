using DevBench.Api.Models;

namespace DevBench.Api.Services.Interfaces;

public interface ISessionService
{
    Task<SessionRecord?> FindAsync(string authorization);
    Task<SessionRecord> RequireAsync(string authorization, string message);
}
