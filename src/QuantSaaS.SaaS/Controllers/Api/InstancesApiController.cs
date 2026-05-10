using Microsoft.AspNetCore.Mvc;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;

namespace QuantSaaS.SaaS.Controllers.Api;

[ApiController]
[Route("api/instances")]
public class InstancesApiController : ControllerBase
{
    private readonly IInstanceService _instances;

    public InstancesApiController(IInstanceService instances)
    {
        _instances = instances;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid userId,
        [FromQuery] string? tab,
        CancellationToken ct)
    {
        var effectiveUserId = userId == Guid.Empty ? DbInitializer.SeedUserId : userId;
        var list = await _instances.GetListAsync(effectiveUserId, tab, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var detail = await _instances.GetDetailAsync(id, ct);
        if (detail is null) return NotFound();
        return Ok(detail);
    }
}
