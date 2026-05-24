using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Tests.Quant;

public class MacroEngineTests
{
    private static Chromosome DefaultChromosome() => Chromosome.DefaultSeed;
    private static SpawnPoint DefaultSpawn() => SpawnPoint.Default;

    [Fact]
    public void Compute_InsufficientFunds_ReturnsNoOrder()
    {
        var input = new MacroEngine.Input
        {
            SpendableUsdt = 5m, // below MinOrderUsdt (10.1)
            CurrentBarTimeMs = 1000L,
            LastMacroBuyTimeMs = 0L,
            Market = MarketState.Normal,
            Config = DefaultChromosome(),
            Spawn = DefaultSpawn(),
        };
        var output = MacroEngine.Compute(input);
        Assert.False(output.ShouldBuy);
        Assert.Equal(0m, output.OrderUsdt);
    }

    [Fact]
    public void Compute_FirstBuy_Triggers()
    {
        var input = new MacroEngine.Input
        {
            SpendableUsdt = 1000m,
            CurrentBarTimeMs = 1_000_000L,
            LastMacroBuyTimeMs = 0L, // never bought before
            Market = MarketState.Normal,
            Config = DefaultChromosome(),
            Spawn = DefaultSpawn(),
        };
        var output = MacroEngine.Compute(input);
        Assert.True(output.ShouldBuy);
        Assert.True(output.OrderUsdt >= 10m);
    }

    [Fact]
    public void Compute_IntervalNotElapsed_ReturnsNoOrder()
    {
        // LastBuy just happened: current − last = 1 ms
        var nowMs = 1_000_000L;
        var input = new MacroEngine.Input
        {
            SpendableUsdt = 1000m,
            CurrentBarTimeMs = nowMs,
            LastMacroBuyTimeMs = nowMs - 1,
            Market = MarketState.Normal,
            Config = DefaultChromosome(), // MacroDcaIntervalDays = 30
            Spawn = DefaultSpawn(),
        };
        var output = MacroEngine.Compute(input);
        Assert.False(output.ShouldBuy);
    }

    [Fact]
    public void Compute_IntervalElapsed_TriggersBuy()
    {
        long intervalMs = (long)(30L * 24 * 3600 * 1000); // 30 days in ms
        var lastBuy = 1_000_000_000L;
        var input = new MacroEngine.Input
        {
            SpendableUsdt = 500m,
            CurrentBarTimeMs = lastBuy + intervalMs + 1,
            LastMacroBuyTimeMs = lastBuy,
            Market = MarketState.Normal,
            Config = DefaultChromosome(),
            Spawn = DefaultSpawn(),
        };
        var output = MacroEngine.Compute(input);
        Assert.True(output.ShouldBuy);
        Assert.Equal("ScheduledDCA", output.Reason);
    }

    [Fact]
    public void Compute_BullMarket_AcceleratesInterval()
    {
        // Bull accel = 1.5, base interval = 30 days
        // Bull effective interval = 30 / 1.5 = 20 days
        // Test with 75% of base (22.5 days): > 20 (bull triggers) but < 30 (normal does not)
        var chromosome = Chromosome.DefaultSeed;
        long baseIntervalMs = (long)(chromosome.MacroDcaIntervalDays * 24 * 3600 * 1000m);
        long testElapsed = (long)(baseIntervalMs * 0.75);
        var lastBuy = 1_000_000_000L;

        var inputNormal = new MacroEngine.Input
        {
            SpendableUsdt = 500m,
            CurrentBarTimeMs = lastBuy + testElapsed,
            LastMacroBuyTimeMs = lastBuy,
            Market = MarketState.Normal,
            Config = chromosome,
            Spawn = DefaultSpawn(),
        };
        var inputBull = inputNormal with { Market = MarketState.Bull };

        var normalOut = MacroEngine.Compute(inputNormal);
        var bullOut = MacroEngine.Compute(inputBull);

        Assert.False(normalOut.ShouldBuy);
        Assert.True(bullOut.ShouldBuy);
    }

    [Fact]
    public void Compute_DeadlineForce_OverridesInterval()
    {
        var spawn = SpawnPoint.Default with
        {
            Capital = new CapitalPolicy { DeadlineYears = 1, MonthlyInjectQuote = 1000m }
        };
        long deadlineMs = (long)(365.25 * 24 * 3600 * 1000);
        var lastBuy = 1_000_000_000L;

        var input = new MacroEngine.Input
        {
            SpendableUsdt = 500m,
            CurrentBarTimeMs = lastBuy + deadlineMs + 1,
            LastMacroBuyTimeMs = lastBuy,
            Market = MarketState.Normal,
            Config = Chromosome.DefaultSeed,
            Spawn = spawn,
        };
        var output = MacroEngine.Compute(input);
        Assert.True(output.ShouldBuy);
        // deadline elapsed ≥ 1 year → "DeadlineForce"
        Assert.Equal("DeadlineForce", output.Reason);
    }

    [Fact]
    public void Compute_OrderSizeClampedToSpendable()
    {
        var chromosome = Chromosome.DefaultSeed;
        // MacroDcaBuyFraction = 0.2 → 20% of spendable
        var input = new MacroEngine.Input
        {
            SpendableUsdt = 30m, // 20% = 6, below MinOrderUsdt → clamp to 30
            CurrentBarTimeMs = 1_000_000L,
            LastMacroBuyTimeMs = 0L,
            Market = MarketState.Normal,
            Config = chromosome,
            Spawn = DefaultSpawn(),
        };
        var output = MacroEngine.Compute(input);
        Assert.True(output.ShouldBuy);
        Assert.InRange(output.OrderUsdt, 10m, 30m); // clamped from (6 → 10.1) to spendable=30
    }
}
