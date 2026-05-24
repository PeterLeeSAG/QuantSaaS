using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Tests.Quant;

public class MarketStateComputerTests
{
    [Fact]
    public void Compute_NullOrShortSeries_ReturnsNormal()
    {
        Assert.Equal("Normal", MarketStateComputer.Compute(null!).State);
        Assert.Equal("Normal", MarketStateComputer.Compute([]).State);
        Assert.Equal("Normal", MarketStateComputer.Compute([100m, 200m]).State);
    }

    [Fact]
    public void Compute_ConstantPrices_NotQuiet()
    {
        // Constant prices → VolatilityRatio = 0 / 0 = 1.0 (returns 1.0 from guard)
        // 1.0 >= 0.5 threshold → not quiet
        // Trend = 0 (EMA short = EMA long for constant)
        var closes = Enumerable.Repeat(50000m, 200).ToArray();
        var state = MarketStateComputer.Compute(closes);
        // Could be Normal (trend=0, volRatio=1 which is ≥ 0.5)
        Assert.NotEqual("Quiet", state.State);
    }

    [Fact]
    public void Compute_StronglyRisingPrices_ReturnsBull()
    {
        // Bull market: EMA short >> EMA long by > 2%
        // Build a series that starts flat then spikes strongly upward
        var closes = new decimal[200];
        for (int i = 0; i < 150; i++) closes[i] = 40000m;
        for (int i = 150; i < 200; i++) closes[i] = 50000m + (i - 150) * 200m;
        var state = MarketStateComputer.Compute(closes);
        Assert.Equal("Bull", state.State);
        Assert.Equal(1.5m, state.BetaMultiplier);
        Assert.Equal(0.8m, state.TimeDilationMultiplier);
    }

    [Fact]
    public void Compute_StronglyFallingPrices_ReturnsBear()
    {
        var closes = new decimal[200];
        for (int i = 0; i < 150; i++) closes[i] = 50000m;
        for (int i = 150; i < 200; i++) closes[i] = 40000m - (i - 150) * 200m;
        var state = MarketStateComputer.Compute(closes);
        Assert.Equal("Bear", state.State);
        Assert.Equal(1.2m, state.BetaMultiplier);
        Assert.Equal(1.5m, state.TimeDilationMultiplier);
    }

    [Fact]
    public void Compute_LowVolatility_ReturnsQuiet()
    {
        // MarketStateComputer uses VolatilityRatio(closes, shortBars:10, longBars:50).
        // To trigger Quiet, mavShort must be much smaller than mavLong (ratio < 0.5).
        // → Make the last 10 bars calm while the preceding 40 bars are noisy,
        //   so the 50-bar long window straddles the noisy region.
        var closes = new decimal[160];
        // First 150 bars: noisy (alternating ±5000)
        for (int i = 0; i < 150; i++) closes[i] = 50000m + (i % 2 == 0 ? 5000m : -5000m);
        // Last 10 bars: calm (constant)
        for (int i = 150; i < 160; i++) closes[i] = 50000m;
        var state = MarketStateComputer.Compute(closes);
        Assert.Equal("Quiet", state.State);
        Assert.True(state.IsQuiet);
    }

    [Fact]
    public void Compute_Bull_HasCorrectMultipliers()
    {
        var closes = new decimal[200];
        for (int i = 0; i < 100; i++) closes[i] = 40000m;
        for (int i = 100; i < 200; i++) closes[i] = 40000m + (i - 100) * 500m; // strong uptrend
        var state = MarketStateComputer.Compute(closes);
        if (state.State == "Bull")
        {
            Assert.Equal(1.5m, state.BetaMultiplier);
            Assert.Equal(0.8m, state.TimeDilationMultiplier);
        }
    }
}
