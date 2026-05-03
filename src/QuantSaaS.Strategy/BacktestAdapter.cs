using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Strategy;

/// <summary>
/// Backtest adapter: runs Step() across a price history to produce BacktestResult.
/// ACL outer circle: converts raw close arrays to StrategyInput and calls the same Step()
/// used in live trading. Iron Rule: no if(isBacktest) branching anywhere.
/// </summary>
public class BacktestAdapter
{
    private readonly BtcSpotStrategy _strategy = new();

    public record BacktestConfig
    {
        public required decimal[] Closes { get; init; }
        public required long[] Timestamps { get; init; }
        public required Chromosome Chromosome { get; init; }
        public required SpawnPoint Spawn { get; init; }
        public long EvalStartMs { get; init; }
    }

    public record BacktestResult
    {
        public decimal Roi { get; init; }
        public decimal MaxDrawdown { get; init; }
        public decimal FinalEquity { get; init; }
        public int TradeCount { get; init; }
        public decimal[] NavCurve { get; init; } = Array.Empty<decimal>();
        public List<(decimal, int)> CashFlows { get; init; } = new();
    }

    public BacktestResult Run(BacktestConfig cfg)
    {
        if (cfg.Closes.Length < 2) return new BacktestResult();

        var portfolio = new PortfolioState
        {
            UsdtBalance = cfg.Spawn.Capital.SeedCapitalUsdt,
            DeadBtc = 0,
            FloatBtc = 0,
            ColdSealedBtc = 0,
            Symbol = "BTCUSDT",
            AggregationPeriod = "1d"
        };

        var runtimeState = new StrategyRuntimeState();
        var navCurve = new List<decimal>();
        var cashFlows = new List<(decimal amount, int barIdx)>();
        int tradeCount = 0;
        int evalStartBarIdx = 0;

        // Find eval start bar
        for (int i = 0; i < cfg.Timestamps.Length; i++)
        {
            if (cfg.Timestamps[i] >= cfg.EvalStartMs) { evalStartBarIdx = i; break; }
        }

        // Track monthly injections
        int lastMonth = -1, lastYear = -1;

        for (int i = 1; i < cfg.Closes.Length; i++)
        {
            var price = cfg.Closes[i];
            if (price <= 0) continue;

            // Monthly injection (apply to USDT balance)
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(cfg.Timestamps[i]).UtcDateTime;
            if (dt.Month != lastMonth || dt.Year != lastYear)
            {
                if (lastMonth != -1) // Skip first bar
                {
                    portfolio.UsdtBalance += cfg.Spawn.Capital.MonthlyInjectUsdt;
                    if (i >= evalStartBarIdx)
                        cashFlows.Add((cfg.Spawn.Capital.MonthlyInjectUsdt, i - evalStartBarIdx));
                }
                lastMonth = dt.Month;
                lastYear = dt.Year;
            }

            // Build input with all closes up to current bar (no look-ahead)
            var closesUpTo = cfg.Closes[..(i + 1)];
            var timestampsUpTo = cfg.Timestamps[..(i + 1)];

            var input = new StrategyInput
            {
                ClosePrices = closesUpTo,
                Timestamps = timestampsUpTo,
                Portfolio = portfolio,
                Market = MarketStateComputer.Compute(closesUpTo),
                Config = cfg.Chromosome,
                Spawn = cfg.Spawn,
                RuntimeState = runtimeState
            };

            var output = _strategy.Step(input);
            runtimeState = output.NewRuntimeState;

            // Execute macro order
            if (output.MacroAction == OrderAction.Buy && output.MacroOrderUsdt >= 10.1m)
            {
                var fee = output.MacroOrderUsdt * cfg.Spawn.Risk.FeeRate;
                var spend = Math.Min(output.MacroOrderUsdt, portfolio.UsdtBalance);
                if (spend >= 10.1m)
                {
                    var btcBought = (spend - fee) / price;
                    portfolio.UsdtBalance -= spend;
                    portfolio.DeadBtc += btcBought;
                    tradeCount++;
                }
            }

            // Execute micro order
            if (output.MicroAction == OrderAction.Buy && output.MicroOrderUsdt >= 10.1m)
            {
                var spend = Math.Min(output.MicroOrderUsdt, portfolio.UsdtBalance);
                if (spend >= 10.1m)
                {
                    var fee = spend * cfg.Spawn.Risk.FeeRate;
                    var btcBought = (spend - fee) / price;
                    portfolio.UsdtBalance -= spend;
                    portfolio.FloatBtc += btcBought;
                    tradeCount++;
                }
            }
            else if (output.MicroAction == OrderAction.Sell && Math.Abs(output.MicroOrderUsdt) >= 10.1m)
            {
                var sellUsdt = Math.Abs(output.MicroOrderUsdt);
                var btcToSell = sellUsdt / price;
                btcToSell = Math.Min(btcToSell, portfolio.FloatBtc);
                if (btcToSell > 0 && btcToSell * price >= 10.1m)
                {
                    portfolio.FloatBtc -= btcToSell;
                    portfolio.UsdtBalance += btcToSell * price * (1 - cfg.Spawn.Risk.FeeRate);
                    tradeCount++;
                }
            }

            // Handle DeadBTC release (SaaS-side ledger only)
            if (output.ReleaseIntent != null)
            {
                var rel = output.ReleaseIntent;
                var actualRelease = Math.Min(rel.ReleaseBtc, portfolio.DeadBtc);
                portfolio.DeadBtc -= actualRelease;
                portfolio.FloatBtc += actualRelease;
            }

            // Record NAV for eval period
            if (i >= evalStartBarIdx)
                navCurve.Add(portfolio.TotalEquity(price));
        }

        if (navCurve.Count == 0) return new BacktestResult();

        var navArr = navCurve.ToArray();
        var maxDD = MathUtils.MaxDrawdown(navArr);
        var roi = ModifiedDietz.Calculate(navArr, cashFlows, navArr.Length);

        return new BacktestResult
        {
            Roi = roi,
            MaxDrawdown = maxDD,
            FinalEquity = navArr[^1],
            TradeCount = tradeCount,
            NavCurve = navArr,
            CashFlows = cashFlows
        };
    }
}
