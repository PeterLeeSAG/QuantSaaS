using QuantSaaS.Core.Models;
using QuantSaaS.Strategy.BtcSpot;

namespace QuantSaaS.Tests.Core;

public class ChromosomeTests
{
    // ── DefaultSeed ───────────────────────────────────────────────────────────

    [Fact]
    public void DefaultSeed_AllFieldsWithinBounds()
    {
        var c = Chromosome.DefaultSeed;
        AssertWithinBounds(c);
    }

    [Fact]
    public void DefaultSeed_ReturnsNewInstanceEachTime()
    {
        var a = Chromosome.DefaultSeed;
        var b = Chromosome.DefaultSeed;
        Assert.NotSame(a, b);
    }

    // ── Clamp ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Clamp_BelowMinBounds_ClampedToMin()
    {
        var c = new Chromosome
        {
            Beta = -100m, Gamma = -100m, SigmaFloor = -100m,
            CoefX1 = -100m, CoefX2 = -100m, CoefX3 = -100m,
            DeltaWeightThreshold = -100m, VolatilityRatioThreshold = -100m,
            MinOrderThreshold = -100m, MacroDcaIntervalDays = -100m,
            MacroDcaBuyFraction = -100m, MacroBullAccelMultiplier = -100m,
        };
        c.Clamp();
        Assert.Equal(Chromosome.Bounds.BetaMin, c.Beta);
        Assert.Equal(Chromosome.Bounds.GammaMin, c.Gamma);
        Assert.Equal(Chromosome.Bounds.SigmaFloorMin, c.SigmaFloor);
        Assert.Equal(Chromosome.Bounds.CoefMin, c.CoefX1);
        Assert.Equal(Chromosome.Bounds.MinOrderMin, c.MinOrderThreshold);
        Assert.Equal(Chromosome.Bounds.MacroDcaDaysMin, c.MacroDcaIntervalDays);
    }

    [Fact]
    public void Clamp_AboveMaxBounds_ClampedToMax()
    {
        var c = new Chromosome
        {
            Beta = 9999m, Gamma = 9999m, SigmaFloor = 9999m,
            CoefX1 = 9999m, CoefX2 = 9999m, CoefX3 = 9999m,
            DeltaWeightThreshold = 9999m, VolatilityRatioThreshold = 9999m,
            MinOrderThreshold = 9999m, MacroDcaIntervalDays = 9999m,
            MacroDcaBuyFraction = 9999m, MacroBullAccelMultiplier = 9999m,
        };
        c.Clamp();
        Assert.Equal(Chromosome.Bounds.BetaMax, c.Beta);
        Assert.Equal(Chromosome.Bounds.GammaMax, c.Gamma);
        Assert.Equal(Chromosome.Bounds.SigmaFloorMax, c.SigmaFloor);
        Assert.Equal(Chromosome.Bounds.CoefMax, c.CoefX1);
        Assert.Equal(Chromosome.Bounds.MinOrderMax, c.MinOrderThreshold);
        Assert.Equal(Chromosome.Bounds.MacroDcaDaysMax, c.MacroDcaIntervalDays);
    }

    [Fact]
    public void Clamp_ValidValues_Unchanged()
    {
        var seed = Chromosome.DefaultSeed;
        seed.Clamp();
        Assert.Equal(1.0m, seed.Beta);
        Assert.Equal(0.5m, seed.Gamma);
    }

    [Fact]
    public void Clamp_ReturnsThis()
    {
        var c = Chromosome.DefaultSeed;
        var result = c.Clamp();
        Assert.Same(c, result);
    }

    // ── DeepClone ─────────────────────────────────────────────────────────────

    [Fact]
    public void DeepClone_ReturnsDifferentInstance()
    {
        var c = Chromosome.DefaultSeed;
        var clone = c.DeepClone();
        Assert.NotSame(c, clone);
    }

    [Fact]
    public void DeepClone_CopiesAllFields()
    {
        var c = new Chromosome
        {
            Beta = 2.5m, Gamma = 1.0m, SigmaFloor = 0.05m,
            CoefX1 = -1m, CoefX2 = 1.5m, CoefX3 = -0.5m,
            DeltaWeightThreshold = 0.07m, VolatilityRatioThreshold = 2.0m,
            MinOrderThreshold = 15m, MacroDcaIntervalDays = 14m,
            MacroDcaBuyFraction = 0.3m, MacroBullAccelMultiplier = 2.0m,
        };
        var clone = c.DeepClone();
        Assert.Equal(c.Beta, clone.Beta);
        Assert.Equal(c.Gamma, clone.Gamma);
        Assert.Equal(c.SigmaFloor, clone.SigmaFloor);
        Assert.Equal(c.CoefX1, clone.CoefX1);
        Assert.Equal(c.CoefX2, clone.CoefX2);
        Assert.Equal(c.CoefX3, clone.CoefX3);
        Assert.Equal(c.DeltaWeightThreshold, clone.DeltaWeightThreshold);
        Assert.Equal(c.VolatilityRatioThreshold, clone.VolatilityRatioThreshold);
        Assert.Equal(c.MinOrderThreshold, clone.MinOrderThreshold);
        Assert.Equal(c.MacroDcaIntervalDays, clone.MacroDcaIntervalDays);
        Assert.Equal(c.MacroDcaBuyFraction, clone.MacroDcaBuyFraction);
        Assert.Equal(c.MacroBullAccelMultiplier, clone.MacroBullAccelMultiplier);
    }

    [Fact]
    public void DeepClone_MutatingCloneDoesNotAffectOriginal()
    {
        var c = Chromosome.DefaultSeed;
        var clone = c.DeepClone();
        clone.Beta = 9999m;
        Assert.Equal(1.0m, c.Beta);
    }

    private static void AssertWithinBounds(Chromosome c)
    {
        Assert.InRange(c.Beta, Chromosome.Bounds.BetaMin, Chromosome.Bounds.BetaMax);
        Assert.InRange(c.Gamma, Chromosome.Bounds.GammaMin, Chromosome.Bounds.GammaMax);
        Assert.InRange(c.SigmaFloor, Chromosome.Bounds.SigmaFloorMin, Chromosome.Bounds.SigmaFloorMax);
        Assert.InRange(c.CoefX1, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax);
        Assert.InRange(c.CoefX2, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax);
        Assert.InRange(c.CoefX3, Chromosome.Bounds.CoefMin, Chromosome.Bounds.CoefMax);
        Assert.InRange(c.DeltaWeightThreshold, Chromosome.Bounds.ThresholdMin, Chromosome.Bounds.ThresholdMax);
        Assert.InRange(c.MinOrderThreshold, Chromosome.Bounds.MinOrderMin, Chromosome.Bounds.MinOrderMax);
        Assert.InRange(c.MacroDcaIntervalDays, Chromosome.Bounds.MacroDcaDaysMin, Chromosome.Bounds.MacroDcaDaysMax);
        Assert.InRange(c.MacroDcaBuyFraction, Chromosome.Bounds.MacroDcaBuyFractionMin, Chromosome.Bounds.MacroDcaBuyFractionMax);
    }
}

public class BtcChromosomeTests
{
    [Fact]
    public void DefaultSeed_MonthlyMacroBuyQuote_IsReasonable()
    {
        var c = BtcChromosome.DefaultSeed;
        Assert.Equal(500m, c.MonthlyMacroBuyQuote);
    }

    [Fact]
    public void Clamp_MonthlyMacroBuyQuote_BelowMin_ClampedToMin()
    {
        var c = BtcChromosome.DefaultSeed;
        c.MonthlyMacroBuyQuote = 0m; // below 10
        c.Clamp();
        Assert.InRange(c.MonthlyMacroBuyQuote, 10m, 10001m);
    }

    [Fact]
    public void Clamp_MonthlyMacroBuyQuote_AboveMax_ClampedToMax()
    {
        var c = BtcChromosome.DefaultSeed;
        c.MonthlyMacroBuyQuote = 999999m;
        c.Clamp();
        Assert.InRange(c.MonthlyMacroBuyQuote, 0m, 10001m);
    }

    [Fact]
    public void Clamp_BaseParametersAlsoClamped()
    {
        var c = BtcChromosome.DefaultSeed;
        c.Beta = 999m;
        c.Clamp();
        Assert.Equal(Chromosome.Bounds.BetaMax, c.Beta);
    }
}

public class InstrumentTests
{
    [Fact]
    public void BtcUsdt_HasCorrectDefaults()
    {
        var inst = Instrument.BtcUsdt();
        Assert.Equal("BTC/USDT", inst.Symbol);
        Assert.Equal("USDT", inst.QuoteCurrency);
        Assert.Equal(0, inst.SettlementDays);
        Assert.True(inst.FractionAllowed);
    }

    [Fact]
    public void UsStock_HasCorrectDefaults()
    {
        var inst = Instrument.UsStock("AAPL");
        Assert.Equal("AAPL", inst.Symbol);
        Assert.Equal("USD", inst.QuoteCurrency);
        Assert.Equal(2, inst.SettlementDays);
        Assert.False(inst.FractionAllowed);
    }

    [Fact]
    public void UsEtf_HasCorrectDefaults()
    {
        var inst = Instrument.UsEtf("SPY");
        Assert.Equal("SPY", inst.Symbol);
        Assert.Equal("USD", inst.QuoteCurrency);
        Assert.Equal(2, inst.SettlementDays);
    }

    [Fact]
    public void BtcUsdt_CustomExchange()
    {
        var inst = Instrument.BtcUsdt("BINANCE");
        Assert.Equal("BINANCE", inst.Exchange);
    }
}

public class MarketStateTests
{
    [Fact]
    public void Default_IsNormalState()
    {
        var state = MarketState.Default;
        Assert.Equal("Normal", state.State);
        Assert.False(state.IsQuiet);
        Assert.Equal(1.0m, state.BetaMultiplier);
        Assert.Equal(1.0m, state.TimeDilationMultiplier);
    }

    [Fact]
    public void Quiet_HasIsQuietTrue()
    {
        var state = MarketState.Quiet;
        Assert.True(state.IsQuiet);
        Assert.Equal("Quiet", state.State);
    }

    [Fact]
    public void Bull_HasCorrectLabel()
    {
        var state = MarketState.Bull;
        Assert.Equal("Bull", state.State);
        Assert.False(state.IsQuiet);
    }

    [Fact]
    public void Bear_HasCorrectLabel()
    {
        var state = MarketState.Bear;
        Assert.Equal("Bear", state.State);
        Assert.False(state.IsQuiet);
    }

    [Fact]
    public void WithExpression_UpdatesSingleField()
    {
        var bull = MarketState.Bull with { BetaMultiplier = 1.5m };
        Assert.Equal("Bull", bull.State);
        Assert.Equal(1.5m, bull.BetaMultiplier);
    }
}

public class SpawnPointTests
{
    [Fact]
    public void Default_HasReasonableCapitalPolicy()
    {
        var spawn = SpawnPoint.Default;
        Assert.Equal(1000m, spawn.Capital.MonthlyInjectQuote);
        Assert.Equal(4, spawn.Capital.DeadlineYears);
    }

    [Fact]
    public void SampleRandom_ReturnsVariedValues()
    {
        var rng = new Random(42);
        var s1 = SpawnPoint.SampleRandom(rng);
        var s2 = SpawnPoint.SampleRandom(rng);
        // Two random samples should differ (extremely unlikely to match)
        Assert.True(s1.Capital.MonthlyInjectQuote != s2.Capital.MonthlyInjectQuote ||
                    s1.Capital.DeadlineYears != s2.Capital.DeadlineYears);
    }

    [Fact]
    public void SampleRandom_MonthlyInject_InExpectedRange()
    {
        var rng = new Random(0);
        for (int i = 0; i < 20; i++)
        {
            var spawn = SpawnPoint.SampleRandom(rng);
            Assert.InRange(spawn.Capital.MonthlyInjectQuote, 500m, 2001m);
        }
    }
}
