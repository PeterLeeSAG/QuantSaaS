using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Strategy;

/// <summary>
/// BTC spot strategy using Sigmoid Dynamic Balance micro-engine + DCA macro-engine.
/// Iron Rule: Pure function - no I/O, no timers, no DB access.
/// Same Step() used for both backtest and live trading.
/// </summary>
public class BtcSpotStrategy : IPureStrategy
{
    public string StrategyId => "btc-spot-sigmoid-v1";
    public string DisplayName => "Dynamic Balance Strategy";
    public string Version => "1.0.0";
    public bool IsSpotOnly => true;

    private const int MinBarsRequired = 120;

    public StrategyOutput Step(StrategyInput input)
    {
        // Data sufficiency check
        if (input.ClosePrices.Length < MinBarsRequired)
            return new StrategyOutput { NewRuntimeState = input.RuntimeState };

        var price = input.CurrentPrice;
        if (price <= 0) return new StrategyOutput { NewRuntimeState = input.RuntimeState };

        var portfolio = input.Portfolio;
        var cfg = input.Config;
        var spawn = input.Spawn;
        var market = input.Market;

        // Compute derived portfolio metrics
        var totalEquity = portfolio.TotalEquity(price);
        var spendableUsdt = portfolio.SpendableUsdt(price, spawn.Capital.MicroReservePct);
        var currentMicroWeight = portfolio.CurrentMicroWeight(price);

        // === Macro engine: DCA accumulation ===
        var macroOut = MacroEngine.Compute(new MacroEngine.Input
        {
            SpendableUsdt = spendableUsdt,
            CurrentBarTimeMs = input.Timestamps.Length > 0 ? input.Timestamps[^1] : 0,
            LastMacroBuyTimeMs = input.RuntimeState.LastMacroBuyTime,
            Market = market,
            Config = cfg,
            Spawn = spawn
        });

        // === Micro engine: Sigmoid dynamic balance ===
        var microOut = SigmoidMicroEngine.Compute(new SigmoidMicroEngine.Input
        {
            ClosePrices = input.ClosePrices,
            CurrentWeight = currentMicroWeight,
            TotalEquity = totalEquity,
            Config = cfg,
            Market = market
        });

        // === DeadBTC release logic ===
        // Check if micro wants to sell but FloatBTC is insufficient
        DeadReleaseIntent? releaseIntent = null;
        decimal effectiveMicroOrder = microOut.OrderUsdt;

        if (microOut.OrderUsdt < -cfg.MinOrderThreshold)
        {
            var requiredSellUsdt = Math.Abs(microOut.OrderUsdt);
            var requiredBtc = requiredSellUsdt / price;

            if (portfolio.FloatBtc < requiredBtc)
            {
                var hardRelease = DeadReleaseEngine.ComputeHardRelease(new DeadReleaseEngine.Input
                {
                    Portfolio = portfolio,
                    Config = cfg,
                    Spawn = spawn,
                    CurrentBarTimeMs = input.Timestamps.Length > 0 ? input.Timestamps[^1] : 0,
                    RequiredSellUsdt = requiredSellUsdt,
                    CurrentPrice = price
                });

                if (hardRelease.ReleaseBtc > 0)
                    releaseIntent = new DeadReleaseIntent
                    {
                        ReleaseBtc = hardRelease.ReleaseBtc,
                        Reason = hardRelease.Reason,
                        IsSoftRelease = false
                    };
            }
        }
        else if (portfolio.DeadBtc > 0 && currentMicroWeight < 0.05m)
        {
            // Periodic soft release when float is very low
            var softRelease = DeadReleaseEngine.ComputeSoftRelease(new DeadReleaseEngine.Input
            {
                Portfolio = portfolio,
                Config = cfg,
                Spawn = spawn,
                CurrentBarTimeMs = input.Timestamps.Length > 0 ? input.Timestamps[^1] : 0,
                CurrentPrice = price
            });

            if (softRelease.ReleaseBtc > 0)
                releaseIntent = new DeadReleaseIntent
                {
                    ReleaseBtc = softRelease.ReleaseBtc,
                    Reason = softRelease.Reason,
                    IsSoftRelease = true
                };
        }

        // Update runtime state
        var newRuntime = new StrategyRuntimeState
        {
            LastProcessedBarTime = input.Timestamps.Length > 0 ? input.Timestamps[^1] : 0,
            TotalMacroSpentUsdt = input.RuntimeState.TotalMacroSpentUsdt +
                                   (macroOut.ShouldBuy ? macroOut.OrderUsdt : 0),
            LastMacroBuyTime = macroOut.ShouldBuy
                ? (input.Timestamps.Length > 0 ? input.Timestamps[^1] : input.RuntimeState.LastMacroBuyTime)
                : input.RuntimeState.LastMacroBuyTime,
            CachedEmaShort = input.RuntimeState.CachedEmaShort,
            CachedEmaLong = input.RuntimeState.CachedEmaLong
        };

        return new StrategyOutput
        {
            MacroOrderUsdt = macroOut.ShouldBuy ? macroOut.OrderUsdt : 0,
            MacroAction = macroOut.ShouldBuy ? OrderAction.Buy : OrderAction.None,
            MicroOrderUsdt = microOut.OrderUsdt,
            MicroAction = microOut.OrderUsdt > cfg.MinOrderThreshold ? OrderAction.Buy
                        : microOut.OrderUsdt < -cfg.MinOrderThreshold ? OrderAction.Sell
                        : OrderAction.None,
            TargetWeight = microOut.TargetWeight,
            Signal = microOut.Signal,
            VolatilityRatio = microOut.VolatilityRatio,
            ReleaseIntent = releaseIntent,
            NewRuntimeState = newRuntime
        };
    }
}
