using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuantSaaS.Core.Models;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.WebSocket;
using QuantSaaS.Strategy;

namespace QuantSaaS.SaaS.Services;

/// <summary>
/// Cron tick driver: every minute scans all RUNNING instances
/// and calls the pure Step() function if a new bar is available.
/// Iron Rule: Same Step() used for backtest and live.
/// </summary>
public class CronTickService : BackgroundService
{
    private readonly InstanceManager _instanceManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WsHub _wsHub;
    private readonly ILogger<CronTickService> _logger;
    private readonly BtcSpotStrategy _strategy = new();

    public CronTickService(
        InstanceManager instanceManager,
        IServiceScopeFactory scopeFactory,
        WsHub wsHub,
        ILogger<CronTickService> logger)
    {
        _instanceManager = instanceManager;
        _scopeFactory = scopeFactory;
        _wsHub = wsHub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            var ids = _instanceManager.GetRunningInstanceIds().ToList();
            var tasks = ids.Select(id => ProcessInstanceTickAsync(id, stoppingToken));
            await Task.WhenAll(tasks);
        }
    }

    private async Task ProcessInstanceTickAsync(Guid instanceId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuantDbContext>();

        try
        {
            // Step 1: Idempotency check
            var portfolioEntity = await db.PortfolioStates.FindAsync([instanceId], ct);
            var instanceEntity = await db.StrategyInstances.FindAsync([instanceId], ct);
            if (portfolioEntity == null || instanceEntity == null) return;

            var latestBar = await db.KLines
                .Where(k => k.Symbol == instanceEntity.Symbol && k.Interval == instanceEntity.AggregationPeriod)
                .OrderByDescending(k => k.OpenTime)
                .FirstOrDefaultAsync(ct);

            if (latestBar == null || latestBar.OpenTime <= portfolioEntity.LastProcessedBarTime)
                return; // Already processed

            // Step 2: Load recent bars for strategy input
            var recentBars = await db.KLines
                .Where(k => k.Symbol == instanceEntity.Symbol && k.Interval == instanceEntity.AggregationPeriod)
                .OrderByDescending(k => k.OpenTime)
                .Take(1500)
                .OrderBy(k => k.OpenTime)
                .ToListAsync(ct);

            var closes = recentBars.Select(b => b.Close).ToArray();
            var timestamps = recentBars.Select(b => b.OpenTime).ToArray();

            // Step 3: Load champion params
            var champion = await db.GeneRecords
                .Where(g => g.StrategyId == _strategy.StrategyId && g.Role == "champion")
                .OrderByDescending(g => g.PromotedAt)
                .FirstOrDefaultAsync(ct);

            var chromosome = champion != null
                ? TryDeserializeChromosome(champion.ParamPackJson) ?? Chromosome.DefaultSeed
                : Chromosome.DefaultSeed;

            var spawnPoint = champion != null
                ? TryDeserializeSpawn(champion.ParamPackJson) ?? SpawnPoint.Default
                : SpawnPoint.Default;

            // Step 4: Load runtime state
            var runtimeEntity = await db.RuntimeStates.FindAsync([instanceId], ct);
            var runtimeState = runtimeEntity != null
                ? JsonSerializer.Deserialize<StrategyRuntimeState>(runtimeEntity.StateJson) ?? new()
                : new StrategyRuntimeState();

            // Step 5: Build portfolio snapshot
            var portfolio = new PortfolioState
            {
                UsdtBalance = portfolioEntity.UsdtBalance,
                DeadBtc = portfolioEntity.DeadBtc,
                FloatBtc = portfolioEntity.FloatBtc,
                ColdSealedBtc = portfolioEntity.ColdSealedBtc,
                LastProcessedBarTime = portfolioEntity.LastProcessedBarTime,
                Symbol = instanceEntity.Symbol,
                AggregationPeriod = instanceEntity.AggregationPeriod
            };

            // Step 6: Build StrategyInput and call pure Step()
            var input = new StrategyInput
            {
                ClosePrices = closes,
                Timestamps = timestamps,
                Portfolio = portfolio,
                Market = QuantSaaS.Quant.MarketStateComputer.Compute(closes),
                Config = chromosome,
                Spawn = spawnPoint,
                RuntimeState = runtimeState
            };

            var output = _strategy.Step(input);

            // Step 7: Persist runtime state
            if (runtimeEntity == null)
            {
                db.RuntimeStates.Add(new RuntimeStateEntity
                {
                    InstanceId = instanceId,
                    StateJson = JsonSerializer.Serialize(output.NewRuntimeState),
                    LastUpdatedBarTime = latestBar.OpenTime
                });
            }
            else
            {
                runtimeEntity.StateJson = JsonSerializer.Serialize(output.NewRuntimeState);
                runtimeEntity.LastUpdatedBarTime = latestBar.OpenTime;
            }

            // Step 8: Handle DeadBTC release (SaaS-side ledger only, no Agent command)
            if (output.ReleaseIntent != null)
            {
                portfolioEntity.DeadBtc = Math.Max(0, portfolioEntity.DeadBtc - output.ReleaseIntent.ReleaseBtc);
                portfolioEntity.FloatBtc += output.ReleaseIntent.ReleaseBtc;
                db.AuditLogs.Add(new AuditLogEntity
                {
                    InstanceId = instanceId,
                    EventType = output.ReleaseIntent.IsSoftRelease ? "DEAD_RELEASE_SOFT" : "DEAD_RELEASE_HARD",
                    PayloadJson = JsonSerializer.Serialize(output.ReleaseIntent)
                });
            }

            // Step 9: Build and dispatch TradeCommands
            var currentPrice = closes.Length > 0 ? closes[^1] : 0;

            if (output.MacroAction == OrderAction.Buy && output.MacroOrderUsdt >= chromosome.MinOrderThreshold)
            {
                await DispatchCommandAsync(db, instanceEntity, instanceId, output.MacroOrderUsdt,
                    "BUY", "MACRO", "DEAD_STACK", currentPrice, latestBar.OpenTime, ct);
            }

            if (output.MicroAction == OrderAction.Buy && output.MicroOrderUsdt >= chromosome.MinOrderThreshold)
            {
                await DispatchCommandAsync(db, instanceEntity, instanceId, output.MicroOrderUsdt,
                    "BUY", "MICRO", "FLOATING", currentPrice, latestBar.OpenTime, ct);
            }
            else if (output.MicroAction == OrderAction.Sell && Math.Abs(output.MicroOrderUsdt) >= chromosome.MinOrderThreshold)
            {
                await DispatchCommandAsync(db, instanceEntity, instanceId, Math.Abs(output.MicroOrderUsdt),
                    "SELL", "MICRO", "FLOATING", currentPrice, latestBar.OpenTime, ct);
            }

            // Step 10: Update LastProcessedBarTime
            portfolioEntity.LastProcessedBarTime = latestBar.OpenTime;
            portfolioEntity.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tick failed for instance {Id}", instanceId);
            var db2 = scope.ServiceProvider.GetRequiredService<QuantDbContext>();
            var inst = await db2.StrategyInstances.FindAsync([instanceId], ct);
            if (inst != null) { inst.Status = "ERROR"; await db2.SaveChangesAsync(ct); }
        }
    }

    private async Task DispatchCommandAsync(
        QuantDbContext db,
        StrategyInstanceEntity instance,
        Guid instanceId,
        decimal amountUsdt,
        string action,
        string engine,
        string lotType,
        decimal currentPrice,
        long barTime,
        CancellationToken ct)
    {
        var clientOrderId = $"inst{instanceId:N}-{engine.ToLower()}-{barTime}";
        var cmd = new TradeCommand
        {
            ClientOrderId = clientOrderId,
            Action = action,
            Engine = engine,
            Symbol = instance.Symbol,
            AmountUsdt = action == "BUY" ? amountUsdt : null,
            QtyAsset = action == "SELL" ? amountUsdt / currentPrice : null,
            LotType = lotType
        };

        db.SpotExecutions.Add(new SpotExecutionEntity
        {
            InstanceId = instanceId,
            ClientOrderId = clientOrderId,
            Status = "pending",
            LotType = lotType,
            CommandJson = JsonSerializer.Serialize(cmd)
        });

        if (!_wsHub.SendToAgent(instance.UserId, cmd))
            _logger.LogWarning("Agent disconnected for user {UserId}, command {OrderId} queued", instance.UserId, clientOrderId);
    }

    private static Chromosome? TryDeserializeChromosome(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("btc_spot_config", out var el))
                return JsonSerializer.Deserialize<Chromosome>(el.GetRawText());
        }
        catch { }
        return null;
    }

    private static SpawnPoint? TryDeserializeSpawn(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("spawn_point", out var el))
                return JsonSerializer.Deserialize<SpawnPoint>(el.GetRawText());
        }
        catch { }
        return null;
    }
}
