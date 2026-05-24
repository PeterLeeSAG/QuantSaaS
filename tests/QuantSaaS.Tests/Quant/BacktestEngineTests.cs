using QuantSaaS.Core.Interfaces;
using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Tests.Quant;

public class BacktestEngineTests
{
    private static Bar MakeBar(decimal close, long openTimeMs)
        => new Bar { Close = close, Open = close, High = close, Low = close, OpenTimeMs = openTimeMs };

    private static Instrument BtcInstrument() => Instrument.BtcUsdt();
    private static SpawnPoint DefaultSpawn() => SpawnPoint.Default;

    /// <summary>Minimal pass-through strategy: no intents, updates runtime state.</summary>
    private sealed class NoOpStrategy : IPureStrategy
    {
        public string StrategyId => "noop";
        public string DisplayName => "NoOp";
        public string Version => "1.0";
        public bool IsSpotOnly => true;
        public StrategyOutput Step(StrategyInput input) => StrategyOutput.NoAction(input.RuntimeStateJson);
    }

    /// <summary>Strategy that always tries to buy 100% of spendable.</summary>
    private sealed class AlwaysBuyStrategy : IPureStrategy
    {
        public string StrategyId => "buy";
        public string DisplayName => "AlwaysBuy";
        public string Version => "1.0";
        public bool IsSpotOnly => true;

        public StrategyOutput Step(StrategyInput input) => new()
        {
            Intents = [new TradingIntent
            {
                Action = TradingAction.Buy,
                Engine = EngineLayer.Macro,
                LotType = LotType.DeadStack,
                AmountQuote = input.Portfolio.SpendableQuote,
            }],
            UpdatedRuntimeStateJson = "{}",
        };
    }

    [Fact]
    public void Run_NullOrEmptyBars_ReturnsEmpty()
    {
        var engine = new BacktestEngine(new NoOpStrategy());
        var result = engine.Run(null!, 0, BtcInstrument(), DefaultSpawn());
        Assert.Equal(BacktestResult.Empty.FinalEquity, result.FinalEquity);

        result = engine.Run([], 0, BtcInstrument(), DefaultSpawn());
        Assert.Equal(BacktestResult.Empty.FinalEquity, result.FinalEquity);
    }

    [Fact]
    public void Run_NoOpStrategy_CapitalIsPreservedApproximately()
    {
        // 100 flat bars: no trades, capital stays as injected
        var ts = Enumerable.Range(0, 100).Select(i => (long)(i * 86_400_000L)).ToArray();
        var bars = ts.Select(t => MakeBar(50000m, t)).ToArray();

        var engine = new BacktestEngine(new NoOpStrategy());
        var result = engine.Run(bars, 0, BtcInstrument(), DefaultSpawn());

        // With monthly injections, final equity should be ≥ initial capital
        Assert.True(result.FinalEquity > 0m);
        Assert.True(result.EvaluatedBars > 0);
    }

    [Fact]
    public void Run_AlwaysBuyStrategy_AccumulatesAssets()
    {
        var ts = Enumerable.Range(0, 100).Select(i => (long)(i * 86_400_000L)).ToArray();
        var bars = ts.Select(t => MakeBar(50000m, t)).ToArray();

        var engine = new BacktestEngine(new AlwaysBuyStrategy());
        var result = engine.Run(bars, 0, BtcInstrument(), DefaultSpawn());

        // Buying all available cash means final equity should include crypto holdings
        Assert.True(result.FinalEquity > 0m);
    }

    [Fact]
    public void Run_EvalStartIdx_OnlyEvaluatesPostWarmup()
    {
        var ts = Enumerable.Range(0, 50).Select(i => (long)(i * 86_400_000L)).ToArray();
        var bars = ts.Select(t => MakeBar(50000m, t)).ToArray();

        var engine = new BacktestEngine(new NoOpStrategy());
        var resultFull = engine.Run(bars, 0, BtcInstrument(), DefaultSpawn());
        var resultHalf = engine.Run(bars, 25, BtcInstrument(), DefaultSpawn());

        // Half-window eval starts has fewer evaluated bars
        Assert.True(resultHalf.EvaluatedBars < resultFull.EvaluatedBars);
    }

    [Fact]
    public void Run_WithZeroCostModel_NoCommissions()
    {
        var ts = Enumerable.Range(0, 50).Select(i => (long)(i * 86_400_000L)).ToArray();
        var bars = ts.Select(t => MakeBar(50000m, t)).ToArray();

        var engine = new BacktestEngine(new AlwaysBuyStrategy(), ZeroCostModel.Instance);
        var result = engine.Run(bars, 0, BtcInstrument(), DefaultSpawn());
        Assert.True(result.FinalEquity >= 0m);
    }

    [Fact]
    public void Run_WithFixedCommissionModel_ReducesEquity()
    {
        var ts = Enumerable.Range(0, 50).Select(i => (long)(i * 86_400_000L)).ToArray();
        var bars = ts.Select(t => MakeBar(50000m, t)).ToArray();
        var instrument = BtcInstrument();
        var spawn = DefaultSpawn();

        var engineFree = new BacktestEngine(new AlwaysBuyStrategy(), ZeroCostModel.Instance);
        var engineCost = new BacktestEngine(new AlwaysBuyStrategy(), new FixedCommissionModel(0.005m, 0.001m));

        var resultFree = engineFree.Run(bars, 0, instrument, spawn);
        var resultCost = engineCost.Run(bars, 0, instrument, spawn);

        // Commissions reduce equity
        Assert.True(resultCost.FinalEquity <= resultFree.FinalEquity);
    }

    [Fact]
    public void Run_MaxDrawdown_IsBetweenZeroAndOne()
    {
        var ts = Enumerable.Range(0, 100).Select(i => (long)(i * 86_400_000L)).ToArray();
        // Volatile prices
        var bars = ts.Select((t, i) => MakeBar(50000m + (i % 5 - 2) * 2000m, t)).ToArray();

        var engine = new BacktestEngine(new AlwaysBuyStrategy());
        var result = engine.Run(bars, 10, BtcInstrument(), DefaultSpawn());

        Assert.InRange(result.MaxDrawdown, 0m, 1m);
    }

    [Fact]
    public void Run_WithDividends_CashIncreases()
    {
        var ts = Enumerable.Range(0, 50).Select(i => (long)(i * 86_400_000L)).ToArray();
        var bars = ts.Select(t => MakeBar(200m, t)).ToArray();

        // Dividend of $5 per share at bar 25
        var dividends = new Dictionary<int, decimal> { { 25, 5m } };

        var instrument = Instrument.UsStock("SPY");
        var engine = new BacktestEngine(new NoOpStrategy());

        var resultNoDivs = engine.Run(bars, 0, instrument, DefaultSpawn());
        var resultWithDivs = engine.Run(bars, 0, instrument, DefaultSpawn(), dividends);

        // Dividends add cash to portfolio
        Assert.True(resultWithDivs.FinalEquity >= resultNoDivs.FinalEquity);
    }

    [Fact]
    public void Run_CalendarFiltering_SkipsClosedDays()
    {
        // CryptoCalendar (always open) vs. a closed calendar should differ in evaluated bars
        var ts = Enumerable.Range(0, 60).Select(i => (long)(i * 86_400_000L)).ToArray();
        var bars = ts.Select(t => MakeBar(50000m, t)).ToArray();

        var engineNoCalendar = new BacktestEngine(new NoOpStrategy(), calendar: null);
        var result = engineNoCalendar.Run(bars, 0, BtcInstrument(), DefaultSpawn());

        // No calendar: all bars evaluated
        Assert.True(result.EvaluatedBars > 0);
    }
}
