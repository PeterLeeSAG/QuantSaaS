using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.SaaS.Models;
using System.Security.Claims;

namespace QuantSaaS.SaaS.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly QuantDbContext _db;

    public DashboardController(QuantDbContext db) => _db = db;

    private Guid? GetUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("data")]
    public async Task<IActionResult> GetDashboardData([FromQuery] Guid instanceId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var inst = await _db.StrategyInstances
            .FirstOrDefaultAsync(i => i.Id == instanceId && i.UserId == userId.Value);
        if (inst == null) return NotFound();

        var portfolio = await _db.PortfolioStates.FindAsync(instanceId);
        if (portfolio == null) return Ok(new DashboardData(instanceId, 0, 0, 0, 0, 0, "Normal", 0, 0));

        var latestBar = await _db.KLines
            .Where(k => k.Symbol == inst.Symbol)
            .OrderByDescending(k => k.OpenTime)
            .FirstOrDefaultAsync();

        var price = latestBar?.Close ?? 50000m;
        var equity = portfolio.UsdtBalance + (portfolio.DeadBtc + portfolio.FloatBtc + portfolio.ColdSealedBtc) * price;

        var tradeCount = await _db.TradeRecords
            .Where(t => t.InstanceId == instanceId)
            .CountAsync();

        return Ok(new DashboardData(
            InstanceId: instanceId,
            UsdtBalance: portfolio.UsdtBalance,
            LongTermHoldings: portfolio.DeadBtc * price,
            ActivePosition: portfolio.FloatBtc * price,
            SealedAssets: portfolio.ColdSealedBtc * price,
            TotalEquity: equity,
            MarketState: "Normal",
            LastDecisionTime: portfolio.LastProcessedBarTime,
            TotalTrades: tradeCount
        ));
    }

    [HttpGet("equity-snapshots")]
    public async Task<IActionResult> GetEquitySnapshots([FromQuery] Guid instanceId, [FromQuery] int days = 30)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var inst = await _db.StrategyInstances
            .FirstOrDefaultAsync(i => i.Id == instanceId && i.UserId == userId.Value);
        if (inst == null) return NotFound();

        var fromMs = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds();

        var bars = await _db.KLines
            .Where(k => k.Symbol == inst.Symbol && k.OpenTime >= fromMs)
            .OrderBy(k => k.OpenTime)
            .ToListAsync();

        var portfolio = await _db.PortfolioStates.FindAsync(instanceId);
        if (portfolio == null || !bars.Any())
            return Ok(Array.Empty<EquityPoint>());

        // Reconstruct equity using the known portfolio state and historical prices
        var points = bars.Select(b =>
        {
            var equity = portfolio.UsdtBalance + (portfolio.DeadBtc + portfolio.FloatBtc + portfolio.ColdSealedBtc) * b.Close;
            return new EquityPoint(b.OpenTime, equity);
        }).ToArray();

        return Ok(points);
    }
}
