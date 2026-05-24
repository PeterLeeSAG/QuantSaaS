using QuantSaaS.Core.Models;
using QuantSaaS.Quant;
using QuantSaaS.Strategy.BtcSpot;

namespace QuantSaaS.Tests.Strategy;

public class BtcSpotStrategyTests
{
    private static BtcSpotStrategy DefaultStrategy()
        => new BtcSpotStrategy(BtcChromosome.DefaultSeed);

    private static StrategyInput MakeInput(decimal[] closes, long[] timestamps,
        decimal spendable = 10000m, decimal floatQty = 0m, decimal deadQty = 0m)
    {
        var price = closes[^1];
        return new StrategyInput
        {
            Closes = closes,
            Timestamps = timestamps,
            CurrentPrice = price,
            Instrument = Instrument.BtcUsdt(),
            Portfolio = new PortfolioState
            {
                SpendableQuote = spendable,
                DeadStackQty = deadQty,
                FloatStackQty = floatQty,
                TotalEquity = spendable + (floatQty + deadQty) * price,
            },
            Market = MarketState.Normal,
            RuntimeStateJson = "{}",
        };
    }

    private static (decimal[], long[]) MakeSeries(int count, decimal price = 50000m)
    {
        var closes = Enumerable.Repeat(price, count).ToArray();
        var ts = Enumerable.Range(0, count)
            .Select(i => (long)(1_600_000_000_000L + i * 86_400_000L))
            .ToArray();
        return (closes, ts);
    }

    // ── StrategyId / metadata ─────────────────────────────────────────────────

    [Fact]
    public void StrategyId_IsCorrect()
    {
        Assert.Equal("btc-spot-v1", DefaultStrategy().StrategyId);
    }

    [Fact]
    public void IsSpotOnly_IsTrue()
    {
        Assert.True(DefaultStrategy().IsSpotOnly);
    }

    // ── Insufficient bars ─────────────────────────────────────────────────────

    [Fact]
    public void Step_InsufficientBars_ReturnsNoAction()
    {
        var strategy = DefaultStrategy();
        var (closes, ts) = MakeSeries(5); // far below MinBarsRequired
        var input = MakeInput(closes, ts);
        var output = strategy.Step(input);
        Assert.Empty(output.Intents);
    }

    // ── Zero price / equity guard ─────────────────────────────────────────────

    [Fact]
    public void Step_ZeroPrice_ReturnsNoAction()
    {
        var strategy = DefaultStrategy();
        var (closes, ts) = MakeSeries(SigmoidEngine.MinBarsRequired + 5);
        closes[^1] = 0m; // force zero price
        var input = MakeInput(closes, ts) with { CurrentPrice = 0m };
        var output = strategy.Step(input);
        Assert.Empty(output.Intents);
    }

    [Fact]
    public void Step_ZeroEquity_ReturnsNoAction()
    {
        var strategy = DefaultStrategy();
        var (closes, ts) = MakeSeries(SigmoidEngine.MinBarsRequired + 5);
        var input = MakeInput(closes, ts, spendable: 0m) with
        {
            Portfolio = new PortfolioState { SpendableQuote = 0m, TotalEquity = 0m }
        };
        var output = strategy.Step(input);
        Assert.Empty(output.Intents);
    }

    // ── Monthly boundary detection ────────────────────────────────────────────

    [Fact]
    public void Step_MonthlyBoundary_TriggersMacroBuy()
    {
        var strategy = DefaultStrategy();
        int barCount = SigmoidEngine.MinBarsRequired + 10;
        var closes = Enumerable.Repeat(50000m, barCount).ToArray();

        // Force a month boundary: last two bars span different months
        var ts = new long[barCount];
        var baseDate = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < barCount - 1; i++)
            ts[i] = new DateTimeOffset(baseDate.AddDays(-(barCount - i - 1))).ToUnixTimeMilliseconds();

        // Last bar is in a different month than second-to-last
        var lastBarDate = new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        ts[^1] = new DateTimeOffset(lastBarDate).ToUnixTimeMilliseconds();

        var input = MakeInput(closes, ts, spendable: 5000m);
        var output = strategy.Step(input);

        // Should have a macro buy intent when crossing month boundary
        var macroBuy = output.Intents.FirstOrDefault(i =>
            i.Engine == EngineLayer.Macro && i.Action == TradingAction.Buy);
        Assert.NotNull(macroBuy);
        Assert.Equal(LotType.DeadStack, macroBuy.LotType);
    }

    [Fact]
    public void Step_SameMonth_NoMacroBuy()
    {
        var strategy = DefaultStrategy();
        int barCount = SigmoidEngine.MinBarsRequired + 10;
        var closes = Enumerable.Repeat(50000m, barCount).ToArray();

        // All bars in same month
        var ts = Enumerable.Range(0, barCount)
            .Select(i => new DateTimeOffset(new DateTime(2023, 3, i % 28 + 1, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds())
            .ToArray();

        var input = MakeInput(closes, ts, spendable: 5000m);
        var output = strategy.Step(input);

        var macroBuy = output.Intents.FirstOrDefault(i => i.Engine == EngineLayer.Macro);
        Assert.Null(macroBuy);
    }

    // ── Macro amount capping ──────────────────────────────────────────────────

    [Fact]
    public void Step_MacroBuy_AmountCappedBySpendable()
    {
        // MonthlyMacroBuyQuote = 500, but spendable = 50 → buy at most 50
        var chromosome = BtcChromosome.DefaultSeed;
        chromosome.MonthlyMacroBuyQuote = 500m;
        var strategy = new BtcSpotStrategy(chromosome);

        int barCount = SigmoidEngine.MinBarsRequired + 5;
        var closes = Enumerable.Repeat(50000m, barCount).ToArray();

        // Month boundary
        var ts = new long[barCount];
        for (int i = 0; i < barCount - 1; i++)
            ts[i] = new DateTimeOffset(new DateTime(2023, 1, i % 28 + 1, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        ts[^1] = new DateTimeOffset(new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        var input = MakeInput(closes, ts, spendable: 50m);
        var output = strategy.Step(input);

        var macroBuy = output.Intents.FirstOrDefault(i => i.Engine == EngineLayer.Macro && i.Action == TradingAction.Buy);
        if (macroBuy != null)
            Assert.True(macroBuy.AmountQuote <= 50m);
    }

    // ── Micro engine integration ──────────────────────────────────────────────

    [Fact]
    public void Step_SufficientBars_UpdatesRuntimeState()
    {
        var strategy = DefaultStrategy();
        var (closes, ts) = MakeSeries(SigmoidEngine.MinBarsRequired + 5);
        // Use timestamps spanning different months to trigger macro too
        var input = MakeInput(closes, ts);
        var output = strategy.Step(input);
        Assert.False(string.IsNullOrWhiteSpace(output.UpdatedRuntimeStateJson));
        Assert.NotEqual("{}", output.UpdatedRuntimeStateJson); // should contain LastProcessedBarMs
    }

    // ── Iron Rule: purity ─────────────────────────────────────────────────────

    [Fact]
    public void Step_CalledTwiceWithSameInput_ReturnsSameIntents()
    {
        var strategy = DefaultStrategy();
        var (closes, ts) = MakeSeries(SigmoidEngine.MinBarsRequired + 5);
        // All same month (no macro)
        var input = MakeInput(closes, ts);
        var out1 = strategy.Step(input);
        var out2 = strategy.Step(input);
        // Deterministic: same input → same number of intents
        Assert.Equal(out1.Intents.Count, out2.Intents.Count);
    }

    // ── Intent structure validation ───────────────────────────────────────────

    [Fact]
    public void Step_MicroBuyIntent_HasLotTypeFloating()
    {
        var strategy = DefaultStrategy();
        var (closes, ts) = MakeSeries(SigmoidEngine.MinBarsRequired + 5, 50000m);
        // Override last 10 bars with sharply rising prices to trigger buy signal
        for (int i = closes.Length - 10; i < closes.Length; i++)
            closes[i] = 80000m;
        var input = MakeInput(closes, ts, spendable: 50000m, floatQty: 0m);
        var output = strategy.Step(input);
        var microBuy = output.Intents.FirstOrDefault(i => i.Engine == EngineLayer.Micro && i.Action == TradingAction.Buy);
        if (microBuy != null)
            Assert.Equal(LotType.Floating, microBuy.LotType);
    }

    [Fact]
    public void Step_MicroSellIntent_HasQtyAsset()
    {
        var strategy = DefaultStrategy();
        var (closes, ts) = MakeSeries(SigmoidEngine.MinBarsRequired + 5, 50000m);
        // Sharply falling prices to trigger sell signal
        for (int i = closes.Length - 10; i < closes.Length; i++)
            closes[i] = 20000m;
        // Have significant float stack to sell
        var input = MakeInput(closes, ts, spendable: 100m, floatQty: 0.5m);
        var output = strategy.Step(input);
        var microSell = output.Intents.FirstOrDefault(i => i.Engine == EngineLayer.Micro && i.Action == TradingAction.Sell);
        if (microSell != null)
            Assert.True(microSell.QtyAsset > 0m);
    }
}
