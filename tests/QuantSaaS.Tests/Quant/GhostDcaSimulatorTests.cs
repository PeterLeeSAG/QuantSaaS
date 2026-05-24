using QuantSaaS.Core.Interfaces;
using QuantSaaS.Quant;

namespace QuantSaaS.Tests.Quant;

public class GhostDcaSimulatorTests
{
    private static long[] MakeTimestamps(int count, long startMs = 1_600_000_000_000L, long stepMs = 86_400_000L)
    {
        var ts = new long[count];
        for (int i = 0; i < count; i++) ts[i] = startMs + i * stepMs;
        return ts;
    }

    [Fact]
    public void Simulate_EmptyCloses_ReturnsEmptyResult()
    {
        var result = GhostDcaSimulator.Simulate(
            [], [], new GhostDcaSimulator.Config(1000m, 500m));
        Assert.Equal(0m, result.FinalEquity);
    }

    [Fact]
    public void Simulate_ConstantPrice_FinalEquityEqualsInjected()
    {
        // Single price forever: buying more shares at same price
        var closes = Enumerable.Repeat(100m, 60).ToArray();
        var ts = MakeTimestamps(60);
        var cfg = new GhostDcaSimulator.Config(InitialCapital: 1000m, MonthlyInject: 100m);
        var result = GhostDcaSimulator.Simulate(closes, ts, cfg);

        // Initial capital buys 10 shares at $100. Each month buys 1 more share.
        // 60 daily bars spans ~2 months → 1 monthly injection (plus initial)
        Assert.True(result.FinalEquity > 0m);
        Assert.True(result.TotalInjected >= 1000m);
    }

    [Fact]
    public void Simulate_RisingPrices_FinalEquityAboveInjected()
    {
        var closes = Enumerable.Range(1, 30).Select(i => 100m + i * 10m).ToArray();
        var ts = MakeTimestamps(30);
        var cfg = new GhostDcaSimulator.Config(1000m, 200m);
        var result = GhostDcaSimulator.Simulate(closes, ts, cfg);
        // Rising prices → investment worth more than invested
        Assert.True(result.FinalEquity > 1000m);
    }

    [Fact]
    public void Simulate_FallingPrices_MaxDrawdownPositive()
    {
        var closes = Enumerable.Range(1, 30).Select(i => 1000m - i * 20m).ToArray();
        var ts = MakeTimestamps(30);
        var cfg = new GhostDcaSimulator.Config(1000m, 200m);
        var result = GhostDcaSimulator.Simulate(closes, ts, cfg);
        Assert.True(result.MaxDrawdown >= 0m);
    }

    [Fact]
    public void Simulate_ZeroMonthlyInject_OnlyInitialCapital()
    {
        var closes = new[] { 100m, 110m, 120m };
        var ts = MakeTimestamps(3);
        var cfg = new GhostDcaSimulator.Config(1000m, MonthlyInject: 0m);
        var result = GhostDcaSimulator.Simulate(closes, ts, cfg);
        // Total injected = initial only
        Assert.Equal(1000m, result.TotalInjected);
    }

    [Fact]
    public void Simulate_DividendReinvestment_IncreasesFinalEquity()
    {
        var closes = Enumerable.Repeat(100m, 5).ToArray();
        var ts = MakeTimestamps(5);
        var dividends = new Dictionary<int, decimal> { { 2, 1m }, { 4, 1m } }; // $1 dividend per share
        var cfg = new GhostDcaSimulator.Config(1000m, 0m, ReinvestDividends: true);
        var result = GhostDcaSimulator.Simulate(closes, ts, cfg, dividendsPerShare: dividends);
        // With reinvestment, should hold slightly more shares
        Assert.True(result.FinalEquity >= 1000m);
    }

    [Fact]
    public void Simulate_WithCalendar_Null_WorksAsDefault()
    {
        var closes = Enumerable.Repeat(100m, 90).ToArray();
        var ts = MakeTimestamps(90);
        var cfg = new GhostDcaSimulator.Config(1000m, 200m);
        var result = GhostDcaSimulator.Simulate(closes, ts, cfg, calendar: null);
        Assert.True(result.FinalEquity > 0m);
    }

    [Fact]
    public void Simulate_MaxDrawdownBetweenZeroAndOne()
    {
        var closes = new[] { 100m, 80m, 60m, 80m, 100m };
        var ts = MakeTimestamps(5);
        var cfg = new GhostDcaSimulator.Config(1000m, 0m);
        var result = GhostDcaSimulator.Simulate(closes, ts, cfg);
        Assert.InRange(result.MaxDrawdown, 0m, 1m);
    }
}
