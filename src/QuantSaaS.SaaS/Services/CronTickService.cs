using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuantSaaS.Core.Models;
using QuantSaaS.Infrastructure.Data;
using QuantSaaS.Infrastructure.WebSocket;
using QuantSaaS.Quant;
using QuantSaaS.Strategy.BtcSpot;

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
            var portfolioEntity = await db.PortfolioStates.FindAsync([instanceId], ct);
            var instanceEntity = await db.StrategyInstances.FindAsync([instanceId], ct);
            if (portfolioEntity == null || instanceEntity == null) return;

            var latestBar = await db.KLines
                .Where(k => k.Symbol == instanceEntity.Symbol && k.Interval == instanceEntity.AggregationPeriod)
                .OrderByDescending(k => k.OpenTime)
                .FirstOrDefaultAsync(ct);

            if (latestBar == null || latestBar.OpenTime <= portfolioEntity.LastProcessedBarTime)
                return;

            var recentBars = await db.KLines
                .Where(k => k.Symbol == instanceEntity.Symbol && k.Interval == instanceEntity.AggregationPeriod)
                .OrderByDescending(k => k.OpenTime)
                .Take(1500)
                .OrderBy(k => k.OpenTime)
                .ToListAsync(ct);

            var closes = recentBars.Select(b => b.Close).ToArray();
            var timestamps = recentBars.Select(b => b.OpenTime).ToArray();

            var champion = await db.GeneRecords
                .Where(g => g.StrategyId == "btc-spot-v1" && g.Role == "champion")
                .OrderByDescending(g => g.PromotedAt)
                .FirstOrDefaultAsync(ct);

            var chromosome = champion != null
                ? TryDeserializeChromosome(champion.ParamPackJson) ?? BtcChromosome.DefaultSeed
                : BtcChromosome.DefaultSeed;

            var spawnPoint = champion != null
                ? TryDeserializeSpawn(champion.ParamPackJson) ?? SpawnPoint.Default
                : SpawnPoint.Default;

            var runtimeEntity = await db.RuntimeStates.FindAsync([instanceId], ct);
            var runtimeStateJson = runtimeEntity?.StateJson ?? "{}";

            var currentPrice = closes.Length > 0 ? closes[^1] : 0m;
            var portfolio = new PortfolioState
            {
                SpendableQuote = portfolioEntity.UsdtBalance,
                DeadStackQty = portfolioEntity.DeadBtc,
                FloatStackQty = portfolioEntity.FloatBtc,
                ColdSealedQty = portfolioEntity.ColdSealedBtc,
                TotalEquity = portfolioEntity.UsdtBalance
                              + (portfolioEntity.DeadBtc + portfolioEntity.FloatBtc + portfolioEntity.ColdSealedBtc) * currentPrice,
                LastProcessedBarMs = portfolioEntity.LastProcessedBarTime
            };

            var instrument = new Instrument
            {
                Symbol = instanceEntity.Symbol,
                AssetClass = AssetClass.Crypto,
                QuoteCurrency = "USDT",
                LotStep = 0.00001m,
                LotMin = 0.00001m,
                TickSize = 0.01m,
                FractionAllowed = true,
                SettlementDays = 0
            };

            var strategy = new BtcSpotStrategy(chromosome);
            var input = new StrategyInput
            {
                Closes = closes,
                Timestamps = timestamps,
                CurrentPrice = currentPrice,
                Instrument = instrument,
                Portfolio = portfolio,
                Market = MarketStateComputer.Compute(closes),
                RuntimeStateJson = runtimeStateJson
            };

            var output = strategy.Step(input);

            if (runtimeEntity == null)
            {
                db.RuntimeStates.Add(new RuntimeStateEntity
                {
                    InstanceId = instanceId,
                    StateJson = output.UpdatedRuntimeStateJson,
                    LastUpdatedBarTime = latestBar.OpenTime
                });
            }
            else
            {
                runtimeEntity.StateJson = output.UpdatedRuntimeStateJson;
                runtimeEntity.LastUpdatedBarTime = latestBar.OpenTime;
            }

            foreach (var intent in output.Intents)
            {
                decimal amount = intent.Action == TradingAction.Buy ? intent.AmountQuote : intent.QtyAsset * currentPrice;
                if (amount < chromosome.MinOrderThreshold) continue;

                var engineLabel = intent.Engine == EngineLayer.Macro ? "MACRO" : "MICRO";
                var lotTypeLabel = intent.LotType == LotType.DeadStack ? "DEAD_STACK" : "FLOATING";
                var action = intent.Action == TradingAction.Buy ? "BUY" : "SELL";

                var clientOrderId = $"inst{instanceId:N}-{engineLabel.ToLower()}-{latestBar.OpenTime}";
                var cmd = new TradeCommand
                {
                    ClientOrderId = clientOrderId,
                    Action = action,
                    Engine = engineLabel,
                    Symbol = instanceEntity.Symbol,
                    AmountQuote = action == "BUY" ? intent.AmountQuote : null,
                    QtyAsset = action == "SELL" ? intent.QtyAsset : null,
                    LotType = lotTypeLabel
                };

                db.SpotExecutions.Add(new SpotExecutionEntity
                {
                    InstanceId = instanceId,
                    ClientOrderId = clientOrderId,
                    Status = "pending",
                    LotType = lotTypeLabel,
                    CommandJson = JsonSerializer.Serialize(cmd)
                });

                if (!_wsHub.SendToAgent(instanceEntity.UserId, cmd))
                    _logger.LogWarning("Agent disconnected for user {UserId}, command {OrderId} queued",
                        instanceEntity.UserId, clientOrderId);
            }

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

    private static BtcChromosome? TryDeserializeChromosome(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("btc_spot_config", out var el))
                return JsonSerializer.Deserialize<BtcChromosome>(el.GetRawText());
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
