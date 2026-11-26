using System.Text.Json.Nodes;
using DevBench.Api.Services;
using DevBench.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DevBench.Api.Controllers;

[ApiController]
[Route("backups")]
public sealed class BackupsController(IBackupService backups, ISessionService sessions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var session = await sessions.RequireAsync(Request.Headers.Authorization.ToString(), "Sign in to view your backups");
        return Ok(await backups.ListAsync(session));
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        // Authenticate before reading the body, as in the desktop API contract.
        var session = await sessions.RequireAsync(Request.Headers.Authorization.ToString(), "Sign in to back up this set");
        JsonObject? raw;
        try { raw = await Request.ReadFromJsonAsync<JsonObject>(HttpContext.RequestAborted); }
        // Thrown for a missing or non-JSON content type and for an unknown charset.
        catch (InvalidOperationException) { throw new ApiException(400, "Invalid request"); }
        return StatusCode(201, await backups.CreateAsync(session, raw ?? throw new ArgumentException("Missing backup")));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Restore(string id)
    {
        var session = await sessions.RequireAsync(Request.Headers.Authorization.ToString(), "Sign in to restore a backup");
        return Ok(await backups.RestoreAsync(session, id));
    }
}
