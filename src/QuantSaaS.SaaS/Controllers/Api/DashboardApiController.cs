using Microsoft.AspNetCore.Mvc;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.Services;

namespace QuantSaaS.SaaS.Controllers.Api;

[ApiController]
[Route("api/dashboard")]
public class DashboardApiController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardApiController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid userId,
        CancellationToken ct)
    {
        var effectiveUserId = userId == Guid.Empty ? DbInitializer.SeedUserId : userId;
        var dto = await _dashboard.GetSummaryAsync(effectiveUserId, ct);
        return Ok(new
        {
            totalEquity      = dto.TotalEquity,
            activeInstances  = dto.ActiveInstances,
            todayTrades      = dto.TodayTrades,
            availableFunds   = dto.AvailableFunds,
            equityCurve      = dto.EquityCurve,
            equityLabels     = dto.EquityLabels,
            instances        = dto.Instances,
            generatedAt      = DateTime.UtcNow
        });
    }
}
