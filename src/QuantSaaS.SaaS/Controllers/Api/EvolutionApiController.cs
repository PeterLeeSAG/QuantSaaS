using Microsoft.AspNetCore.Mvc;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;

namespace QuantSaaS.SaaS.Controllers.Api;

[ApiController]
[Route("api/evolution")]
public class EvolutionApiController : ControllerBase
{
    private readonly IEvolutionService _evolution;

    public EvolutionApiController(IEvolutionService evolution)
    {
        _evolution = evolution;
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks(
        [FromQuery] Guid userId,
        CancellationToken ct)
    {
        var effectiveUserId = userId == Guid.Empty ? DbInitializer.SeedUserId : userId;
        var lab = await _evolution.GetLabAsync(effectiveUserId, ct);
        return Ok(new { tasks = lab.Tasks, champions = lab.Champions });
    }
}
