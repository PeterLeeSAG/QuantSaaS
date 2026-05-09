using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.SaaS.Models;
using QuantSaaS.SaaS.Services;
using System.Security.Claims;

namespace QuantSaaS.SaaS.Controllers;

[ApiController]
[Route("api/v1/instances")]
public class InstancesController : ControllerBase
{
    private readonly QuantDbContext _db;
    private readonly InstanceManager _instanceManager;

    public InstancesController(QuantDbContext db, InstanceManager instanceManager)
    {
        _db = db;
        _instanceManager = instanceManager;
    }

    private Guid? GetUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> ListInstances()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var instances = await _db.StrategyInstances
            .Where(i => i.UserId == userId.Value)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var result = new List<InstanceSummary>();
        foreach (var inst in instances)
        {
            var portfolio = await _db.PortfolioStates.FindAsync(inst.Id);
            decimal? equity = null;
            if (portfolio != null)
            {
                var latestBar = await _db.KLines
                    .Where(k => k.Symbol == inst.Symbol)
                    .OrderByDescending(k => k.OpenTime)
                    .FirstOrDefaultAsync();
                if (latestBar != null)
                    equity = portfolio.UsdtBalance + (portfolio.DeadBtc + portfolio.FloatBtc + portfolio.ColdSealedBtc) * latestBar.Close;
            }
            result.Add(new InstanceSummary(inst.Id, inst.Symbol, inst.Status, inst.AggregationPeriod, inst.FundQuota, inst.CreatedAt, inst.StartedAt, equity));
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateInstance([FromBody] CreateInstanceRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var inst = new StrategyInstanceEntity
        {
            UserId = userId.Value,
            TemplateId = Guid.Empty,
            Symbol = req.Symbol,
            AggregationPeriod = req.AggregationPeriod,
            FundQuota = req.FundQuota,
            Status = "STOPPED"
        };
        _db.StrategyInstances.Add(inst);

        var portfolio = new PortfolioStateEntity
        {
            InstanceId = inst.Id,
            UsdtBalance = req.FundQuota
        };
        _db.PortfolioStates.Add(portfolio);

        await _db.SaveChangesAsync();
        return Ok(new { instanceId = inst.Id });
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] PatchStatusRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var inst = await _db.StrategyInstances
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId.Value);
        if (inst == null) return NotFound();

        if (req.Action == "start")
            await _instanceManager.StartAsync(id);
        else if (req.Action == "stop")
            await _instanceManager.StopAsync(id);

        return Ok(new { status = inst.Status });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInstance(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var inst = await _db.StrategyInstances
            .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId.Value);
        if (inst == null) return NotFound();
        if (inst.Status == "RUNNING")
            return BadRequest(new { error = "Stop the instance before deleting it" });

        _db.StrategyInstances.Remove(inst);
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record PatchStatusRequest(string Action); // start|stop
