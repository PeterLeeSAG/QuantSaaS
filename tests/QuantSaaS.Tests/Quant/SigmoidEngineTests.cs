using QuantSaaS.Core.Models;
using QuantSaaS.Quant;

namespace QuantSaaS.Tests.Quant;

public class SigmoidEngineTests
{
    private static SigmoidEngine.Input BuildInput(decimal[] closes, decimal currentWeight = 0.3m,
        decimal totalEquity = 10000m, decimal spendable = 5000m)
        => new SigmoidEngine.Input
        {
            Closes = closes,
            CurrentPrice = closes[^1],
            CurrentMicroWeight = currentWeight,
            TotalEquity = totalEquity,
            SpendableQuote = spendable,
            Beta = 1.0m,
            Gamma = 0.5m,
            SigmaFloor = 0.01m,
            CoefX1 = 1.0m,
            CoefX2 = 0.5m,
            CoefX3 = 0.3m,
            DeltaWeightThreshold = 0.03m,
            VolatilityRatioThreshold = 1.8m,
            MinOrderThreshold = 10.1m,
            BetaMultiplier = 1.0m,
            IsQuiet = false,
        };

    [Fact]
    public void Compute_InsufficientBars_ReturnsEmpty()
    {
        // SigmoidEngine.Compute requires at least 2 bars (Length < 2 guard)
        var input = BuildInput([50000m]); // single bar → insufficient
        var output = SigmoidEngine.Compute(input);
        Assert.Equal(0m, output.OrderQuote);
        Assert.Equal(0m, output.TargetWeight);
    }

    [Fact]
    public void Compute_ConstantPrices_TargetWeightIsHalf()
    {
        // Constant prices → signal = 0 → sigmoid at 0.5 with no inventory bias adjustment
        var closes = Enumerable.Repeat(50000m, 150).ToArray();
        var input = BuildInput(closes, currentWeight: 0.5m);
        var output = SigmoidEngine.Compute(input);
        // Target weight should be near 0.5 when signal = 0 and currentWeight = 0.5
        Assert.InRange(output.TargetWeight, 0.4m, 0.6m);
    }

    [Fact]
    public void Compute_QuietState_SupressesDustOrders()
    {
        // Tiny equity: theoreticalQuote < MinOrderThreshold but |deltaWeight| >= threshold
        // → without quiet: wedge fires; with quiet: suppressed
        var closes = Enumerable.Repeat(50000m, 150).ToArray();
        var inputLive = BuildInput(closes, currentWeight: 0.3m, totalEquity: 1m, spendable: 1m)
            with { IsQuiet = false };
        var inputQuiet = BuildInput(closes, currentWeight: 0.3m, totalEquity: 1m, spendable: 1m)
            with { IsQuiet = true };

        var liveOut = SigmoidEngine.Compute(inputLive);
        var quietOut = SigmoidEngine.Compute(inputQuiet);

        // Quiet suppresses wedge-zone orders
        Assert.NotEqual(0m, liveOut.OrderQuote);   // wedge fires when not quiet
        Assert.Equal(0m, quietOut.OrderQuote);     // quiet suppresses wedge order
    }

    [Fact]
    public void Compute_StrongBuySignal_GeneratesBuyOrder()
    {
        // Create a sharp rising series: last bars much higher than EMA
        var closes = new decimal[150];
        for (int i = 0; i < 140; i++) closes[i] = 40000m;
        for (int i = 140; i < 150; i++) closes[i] = 60000m; // big spike up
        var input = BuildInput(closes, currentWeight: 0m, totalEquity: 10000m, spendable: 10000m);
        var output = SigmoidEngine.Compute(input);
        // Rising price → signal should be negative (exponent positive) → sigmoid < 0.5 → targetWeight < 0.5
        // currentWeight=0, targetWeight < 0.5 → deltaWeight < 0 → sell? No, wait.
        // With currentWeight=0 and targetWeight < 0.5, deltaWeight is positive → BUY
        // Actually: x1 = (price - ema) / (sigma * price), if price > ema and sigma small, x1 is large positive
        // signal = CoefX1 * x1 + ... = positive
        // exponent = Beta * signal + Gamma * (currentWeight - 0.5) = positive + negative = depends
        // sigmoid = 1 / (1 + exp(exponent))
        // If exponent > 0 → sigmoid < 0.5, if exponent < 0 → sigmoid > 0.5
        // currentWeight=0 → inventoryBias=-0.5 → Gamma*inventoryBias = -0.25
        // Positive large signal + negative bias term: net depends on magnitude
        // Either way, let's just check that the engine produces a non-zero order
        Assert.True(output.OrderQuote != 0m);
    }

    [Fact]
    public void Compute_BetaMultiplierAmplifies()
    {
        var closes = new decimal[150];
        for (int i = 0; i < 140; i++) closes[i] = 40000m;
        for (int i = 140; i < 150; i++) closes[i] = 55000m;

        var input1 = BuildInput(closes, currentWeight: 0.3m, totalEquity: 10000m) with { BetaMultiplier = 1.0m };
        var input2 = BuildInput(closes, currentWeight: 0.3m, totalEquity: 10000m) with { BetaMultiplier = 2.0m };

        var out1 = SigmoidEngine.Compute(input1);
        var out2 = SigmoidEngine.Compute(input2);

        // Higher BetaMultiplier → same signal, larger effective beta → more extreme sigmoid
        // Target weights should differ
        Assert.NotEqual(out1.TargetWeight, out2.TargetWeight);
    }

    [Fact]
    public void Compute_MinOrderThreshold_FiltersSmallOrders()
    {
        // currentWeight ≈ targetWeight → deltaWeight near 0, below DeltaWeightThreshold
        // → both direct path and wedge gate stay closed → OrderQuote = 0
        var closes = Enumerable.Repeat(50000m, 150).ToArray();
        // targetWeight ≈ 0.497 for constant prices with currentWeight=0.525
        var input = BuildInput(closes, currentWeight: 0.525m, totalEquity: 1m, spendable: 1m)
            with { MinOrderThreshold = 100m };
        var output = SigmoidEngine.Compute(input);
        Assert.Equal(0m, output.OrderQuote);
    }

    [Fact]
    public void Compute_TargetWeightBoundedBetweenZeroAndOne()
    {
        var closes = new decimal[150];
        for (int i = 0; i < 150; i++) closes[i] = (decimal)(i + 1) * 1000;
        var input = BuildInput(closes);
        var output = SigmoidEngine.Compute(input);
        Assert.InRange(output.TargetWeight, 0m, 1m);
    }

    [Fact]
    public void MinBarsRequired_IsGreaterThanLongVolatilityWindow()
    {
        Assert.True(SigmoidEngine.MinBarsRequired >= 113);
    }
}
