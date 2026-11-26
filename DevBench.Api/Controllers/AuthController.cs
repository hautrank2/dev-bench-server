using DevBench.Api.Models.Dtos;
using DevBench.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevBench.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("desktop/start")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Start([FromBody] LoginStart input) => Ok(await auth.StartAsync(input));

    [HttpGet("google/callback")]
    public async Task<IActionResult> Callback()
    {
        var query = Request.Query;
        var result = await auth.CallbackAsync(query["state"].ToString(), query["code"].ToString(),
            query.ContainsKey("error"), HttpContext.RequestAborted);
        return new ContentResult { Content = result.Message, ContentType = "text/plain; charset=utf-8", StatusCode = result.Success ? 200 : 400 };
    }

    [HttpPost("desktop/complete")]
    public async Task<IActionResult> Complete([FromBody] LoginComplete input)
    {
        var result = await auth.CompleteAsync(input);
        return result is null ? StatusCode(202, new { pending = true }) : Ok(result);
    }

    [HttpGet("/me")]
    public async Task<IActionResult> Me() => Ok(await auth.MeAsync(Request.Headers.Authorization.ToString()));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await auth.LogoutAsync(Request.Headers.Authorization.ToString());
        return NoContent();
    }
}
