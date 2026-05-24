using System;

namespace QuantSaaS.Quant;

/// <summary>
/// Micro Sigmoid engine: computes the target position weight using Sigmoid dynamics.
/// Design philosophy:
///   Signal = external market force;
///   InventoryBias = spring restoring force (mean-reversion);
///   Beta = spring stiffness (aggressiveness);
///   Gamma = spring enable/disable;
///   VolatilityRatio wedge filter = dust suppression during quiet periods.
/// </summary>
public static class SigmoidEngine
{
    /// <summary>Minimum bar count required for Sigmoid computation.</summary>
    public const int MinBarsRequired = 113; // VolRatioLongBars + 1

    public record Input
    {
        public static int MinBarsRequired => SigmoidEngine.MinBarsRequired;

        public decimal[] Closes { get; init; } = Array.Empty<decimal>();
        public decimal CurrentPrice { get; init; }
        public decimal CurrentMicroWeight { get; init; }
        public decimal TotalEquity { get; init; }
        public decimal SpendableQuote { get; init; }

        // Chromosome parameters
        public decimal Beta { get; init; } = 1.0m;
        public decimal Gamma { get; init; } = 0.5m;
        public decimal SigmaFloor { get; init; } = 0.01m;
        public decimal CoefX1 { get; init; } = 1.0m;
        public decimal CoefX2 { get; init; } = 0.5m;
        public decimal CoefX3 { get; init; } = 0.3m;
        public decimal DeltaWeightThreshold { get; init; } = 0.03m;
        public decimal VolatilityRatioThreshold { get; init; } = 1.8m;
        public decimal MinOrderThreshold { get; init; } = 10.1m;

        // Market state inputs
        public decimal BetaMultiplier { get; init; } = 1.0m;
        public bool IsQuiet { get; init; }
    }

    public record Output
    {
        public decimal TargetWeight { get; init; }
        public decimal Signal { get; init; }
        public decimal TheoreticalQuote { get; init; }
        public decimal OrderQuote { get; init; }    // >0 = BUY, <0 = SELL, 0 = no action
        public decimal VolatilityRatio { get; init; }
    }

    private const int SignalEMABars = 21;
    private const int SignalStdDevBars = 21;
    private const int VolRatioShortBars = 16;
    private const int VolRatioLongBars = 112;

    public static Output Compute(Input inp)
    {
        if (inp.Closes.Length < 2)
            return new Output();

        // Step 1: EMA and σ
        decimal ema = MathUtils.Ema(inp.Closes, SignalEMABars);
        decimal sigma = Math.Max(MathUtils.StdDev(inp.Closes, SignalStdDevBars), inp.SigmaFloor);
        if (sigma == 0m) return new Output();

        decimal price = inp.CurrentPrice;

        // Step 2: Dimensionless signal (3-factor linear model)
        // X1: price deviation from EMA in units of σ
        decimal x1 = (price - ema) / (sigma * price);
        // X2: short-term momentum (log-return over last 5 bars)
        int momentumBars = Math.Min(5, inp.Closes.Length - 1);
        decimal x2 = (decimal)MathUtils.LogReturn(inp.Closes[^(momentumBars + 1)], price);
        // X3: acceleration (change in momentum)
        decimal x3 = inp.Closes.Length > 6
            ? (decimal)MathUtils.LogReturn(inp.Closes[^6], inp.Closes[^2]) - x2
            : 0m;

        decimal signal = inp.CoefX1 * x1 + inp.CoefX2 * x2 + inp.CoefX3 * x3;

        // Step 3: Sigmoid target weight
        decimal effectiveBeta = Math.Max(0.01m, inp.Beta * inp.BetaMultiplier);
        decimal inventoryBias = Math.Clamp(inp.CurrentMicroWeight, 0m, 1m) - 0.5m;
        decimal exponent = effectiveBeta * signal + inp.Gamma * inventoryBias;
        decimal targetWeight = (decimal)(1.0 / (1.0 + Math.Exp((double)exponent)));
        targetWeight = Math.Clamp(targetWeight, 0m, 1m);

        // Step 4: Theoretical order
        decimal deltaWeight = targetWeight - inp.CurrentMicroWeight;
        decimal theoreticalQuote = deltaWeight * inp.TotalEquity;

        // Step 5: Volatility ratio
        decimal volRatio = MathUtils.VolatilityRatio(inp.Closes, VolRatioShortBars, VolRatioLongBars);

        // Step 6: Wedge filter
        decimal orderQuote;
        decimal absTheo = Math.Abs(theoreticalQuote);

        if (absTheo >= inp.MinOrderThreshold)
        {
            orderQuote = theoreticalQuote;
        }
        else if (!inp.IsQuiet &&
                 (Math.Abs(deltaWeight) >= inp.DeltaWeightThreshold || volRatio >= inp.VolatilityRatioThreshold))
        {
            // Force minimum order in the correct direction
            orderQuote = theoreticalQuote >= 0m ? inp.MinOrderThreshold : -inp.MinOrderThreshold;
        }
        else
        {
            orderQuote = 0m;
        }

        return new Output
        {
            TargetWeight = targetWeight,
            Signal = signal,
            TheoreticalQuote = theoreticalQuote,
            OrderQuote = orderQuote,
            VolatilityRatio = volRatio,
        };
    }
}
