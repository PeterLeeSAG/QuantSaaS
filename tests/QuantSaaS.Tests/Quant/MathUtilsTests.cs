using QuantSaaS.Quant;

namespace QuantSaaS.Tests.Quant;

public class MathUtilsTests
{
    // ── EMA ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Ema_NullOrEmptySeries_ReturnsZero()
    {
        Assert.Equal(0m, MathUtils.Ema([], 5));
        Assert.Equal(0m, MathUtils.Ema(null!, 5));
    }

    [Fact]
    public void Ema_PeriodZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MathUtils.Ema([1m, 2m], 0));
    }

    [Fact]
    public void Ema_SingleElement_ReturnsThatElement()
    {
        Assert.Equal(42m, MathUtils.Ema([42m], 3));
    }

    [Fact]
    public void Ema_ConstantSeries_ReturnsConstant()
    {
        var series = Enumerable.Repeat(5m, 20).ToArray();
        Assert.Equal(5m, MathUtils.Ema(series, 10));
    }

    [Fact]
    public void Ema_MonotonicallyIncreasing_ConvergesAboveAverage()
    {
        var series = Enumerable.Range(1, 10).Select(i => (decimal)i).ToArray();
        var ema = MathUtils.Ema(series, 3);
        // EMA of rising series should be between middle and last value
        Assert.True(ema > 5m && ema <= 10m);
    }

    [Fact]
    public void Ema_PeriodLargerThanSeries_UsesFullSeries()
    {
        var series = new[] { 1m, 2m, 3m };
        // Should not throw even when period > length
        var result = MathUtils.Ema(series, 100);
        Assert.True(result > 0m);
    }

    // ── StdDev ────────────────────────────────────────────────────────────────

    [Fact]
    public void StdDev_NullOrShortSeries_ReturnsZero()
    {
        Assert.Equal(0m, MathUtils.StdDev([], 5));
        Assert.Equal(0m, MathUtils.StdDev(null!, 5));
        Assert.Equal(0m, MathUtils.StdDev([1m], 5));
    }

    [Fact]
    public void StdDev_ConstantSeries_ReturnsZero()
    {
        var series = Enumerable.Repeat(10m, 20).ToArray();
        Assert.Equal(0m, MathUtils.StdDev(series, 10));
    }

    [Fact]
    public void StdDev_KnownVariance_CorrectResult()
    {
        // Series [1, 2, 3, 4, 5]: sample std dev = sqrt(2.5) ≈ 1.581
        var series = new[] { 1m, 2m, 3m, 4m, 5m };
        var result = MathUtils.StdDev(series, 5);
        Assert.InRange(result, 1.58m, 1.59m);
    }

    [Fact]
    public void StdDev_UsesLastPeriodValues()
    {
        // First values are outliers; StdDev of last 3 values should be small
        var series = new[] { 1000m, 2000m, 1m, 2m, 3m };
        var result = MathUtils.StdDev(series, 3);
        Assert.True(result < 2m);
    }

    // ── MavAbsChange ─────────────────────────────────────────────────────────

    [Fact]
    public void MavAbsChange_NullOrShort_ReturnsZero()
    {
        Assert.Equal(0m, MathUtils.MavAbsChange([], 5));
        Assert.Equal(0m, MathUtils.MavAbsChange(null!, 5));
        Assert.Equal(0m, MathUtils.MavAbsChange([1m], 5));
    }

    [Fact]
    public void MavAbsChange_ConstantPrices_ReturnsZero()
    {
        var closes = Enumerable.Repeat(100m, 20).ToArray();
        Assert.Equal(0m, MathUtils.MavAbsChange(closes, 10));
    }

    [Fact]
    public void MavAbsChange_LinearSteps_ReturnsStepSize()
    {
        // Prices [10, 12, 14, 16, 18]: all steps = 2
        var closes = new[] { 10m, 12m, 14m, 16m, 18m };
        Assert.Equal(2m, MathUtils.MavAbsChange(closes, 5));
    }

    // ── VolatilityRatio ───────────────────────────────────────────────────────

    [Fact]
    public void VolatilityRatio_InsufficientData_ReturnsOne()
    {
        var closes = Enumerable.Range(1, 50).Select(i => (decimal)i).ToArray();
        Assert.Equal(1.0m, MathUtils.VolatilityRatio(closes, 16, 112));
    }

    [Fact]
    public void VolatilityRatio_ConstantSeries_ReturnsOne()
    {
        var closes = Enumerable.Repeat(100m, 200).ToArray();
        // Both MAVs are 0; returns 1.0 to avoid division by zero
        Assert.Equal(1.0m, MathUtils.VolatilityRatio(closes, 16, 112));
    }

    [Fact]
    public void VolatilityRatio_Result_IsClampedToRange()
    {
        // Volatile short window over calm long window → ratio > 1 but ≤ 3
        var closes = Enumerable.Repeat(100m, 112).ToArray();
        // Replace last 20 with noisy data
        for (int i = 92; i < 112; i++) closes[i] = (decimal)(100 + (i % 2 == 0 ? 10 : -10));
        var ratio = MathUtils.VolatilityRatio(closes, 16, 112);
        Assert.InRange(ratio, 0.1m, 3.0m);
    }

    // ── Clip ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(5.0, 0.0, 10.0, 5.0)]
    [InlineData(-1.0, 0.0, 10.0, 0.0)]
    [InlineData(15.0, 0.0, 10.0, 10.0)]
    public void Clip_ClampsToRange(double value, double lo, double hi, double expected)
    {
        Assert.Equal((decimal)expected, MathUtils.Clip((decimal)value, (decimal)lo, (decimal)hi));
    }

    // ── RoundQuote ────────────────────────────────────────────────────────────

    [Fact]
    public void RoundQuote_RoundsToTwoDecimals()
    {
        Assert.Equal(1.23m, MathUtils.RoundQuote(1.234m));
        Assert.Equal(1.24m, MathUtils.RoundQuote(1.235m));
    }

    // ── LogReturn ─────────────────────────────────────────────────────────────

    [Fact]
    public void LogReturn_ZeroOrNegativePrices_ReturnsZero()
    {
        Assert.Equal(0.0, MathUtils.LogReturn(0m, 100m));
        Assert.Equal(0.0, MathUtils.LogReturn(100m, 0m));
        Assert.Equal(0.0, MathUtils.LogReturn(-1m, 100m));
    }

    [Fact]
    public void LogReturn_EqualPrices_ReturnsZero()
    {
        Assert.Equal(0.0, MathUtils.LogReturn(100m, 100m), precision: 10);
    }

    [Fact]
    public void LogReturn_DoubledPrice_ReturnsLn2()
    {
        var result = MathUtils.LogReturn(100m, 200m);
        Assert.InRange(result, 0.693, 0.694);
    }

    [Fact]
    public void LogReturn_HalvedPrice_ReturnsNegativeLn2()
    {
        var result = MathUtils.LogReturn(200m, 100m);
        Assert.InRange(result, -0.694, -0.693);
    }
}
