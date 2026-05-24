using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantSaaS.SaaS.Models;
using QuantSaaS.SaaS.Services;
using System.Security.Claims;

namespace QuantSaaS.SaaS.Controllers;

[ApiController]
[Route("api/v1/system")]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly InstanceManager _instanceManager;
    private readonly WsHub _wsHub;

    public SystemController(InstanceManager instanceManager, WsHub wsHub)
    {
        _instanceManager = instanceManager;
        _wsHub = wsHub;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool agentConnected = userId != null && Guid.TryParse(userId, out var uid) && _wsHub.IsConnected(uid);

        var status = new SystemStatusResponse(
            EngineStatus: _instanceManager.GetRunningInstanceIds().Any() ? "running" : "paused",
            AgentConnected: agentConnected,
            UserEmail: User.FindFirstValue(ClaimTypes.Email),
            LastCheckedMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        return Ok(status);
    }
}
