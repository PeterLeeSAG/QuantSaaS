using System;
using QuantSaaS.Core.Models;

namespace QuantSaaS.Quant;

/// <summary>
/// Micro engine: Sigmoid dynamic balance system.
/// Signal is external force, InventoryBias is spring restoring force,
/// Beta is spring stiffness, Gamma enables/disables spring.
/// VolatilityRatio wedge filter suppresses dust in quiet periods.
/// </summary>
public static class SigmoidMicroEngine
{
    // Fixed non-evolvable constants
    private const int EmaShortBars = 21;
    private const int EmaLongBars = 89;
    private const int StdDevBars = 21;
    private const int VolRatioShortBars = 16;
    private const int VolRatioLongBars = 112;

    public record Input
    {
        public required decimal[] ClosePrices { get; init; }
        public required decimal CurrentWeight { get; init; }
        public required decimal TotalEquity { get; init; }
        public required Chromosome Config { get; init; }
        public required MarketState Market { get; init; }
    }

    public record Output
    {
        public decimal TargetWeight { get; init; }
        public decimal Signal { get; init; }
        public decimal TheoreticalUsdt { get; init; }
        public decimal OrderUsdt { get; init; }
        public decimal VolatilityRatio { get; init; }
    }

    public static Output Compute(Input input)
    {
        var closes = input.ClosePrices;
        var cfg = input.Config;

        if (closes.Length < EmaLongBars + 5)
            return new Output();

        // Step 1: EMA and sigma
        var emaShort = MathUtils.Ema(closes, EmaShortBars);
        var stdDev = MathUtils.StdDev(closes, StdDevBars);
        var sigma = Math.Max((double)(decimal)stdDev, (double)cfg.SigmaFloor);
        if (sigma == 0) return new Output();

        var currentPrice = closes[^1];

        // Step 2: Dimensionless signal = a*X1 + b*X2 + c*X3
        // X1: z-score of current price vs EMA short (price deviation)
        var x1 = emaShort != 0 ? (currentPrice - emaShort) / ((decimal)sigma * emaShort / 50000m) : 0m;
        x1 = Math.Clamp(x1, -3m, 3m);

        // X2: log-return momentum (EMA short vs EMA long ratio)
        var emaLong = MathUtils.Ema(closes, EmaLongBars);
        var x2 = emaLong != 0 ? (emaShort / emaLong - 1m) * 10m : 0m;
        x2 = Math.Clamp(x2, -3m, 3m);

        // X3: acceleration (rate of change of momentum - difference in recent vs older log returns)
        var recentReturn = closes.Length >= 5 ? (closes[^1] - closes[^5]) / closes[^5] : 0m;
        var olderReturn = closes.Length >= 10 ? (closes[^5] - closes[^10]) / closes[^10] : 0m;
        var x3 = (recentReturn - olderReturn) * 20m;
        x3 = Math.Clamp(x3, -3m, 3m);

        var signal = cfg.CoefX1 * x1 + cfg.CoefX2 * x2 + cfg.CoefX3 * x3;

        // Step 3: Sigmoid target weight
        var effectiveBeta = Math.Max(0.01m, cfg.Beta * input.Market.BetaMultiplier);
        var inventoryBias = Math.Clamp(input.CurrentWeight, 0m, 1m) - 0.5m;
        var exponent = effectiveBeta * signal + cfg.Gamma * inventoryBias;
        var targetWeight = (decimal)(1.0 / (1.0 + Math.Exp((double)exponent)));
        targetWeight = Math.Clamp(targetWeight, 0m, 1m);

        // Step 4: Theoretical order
        var deltaWeight = targetWeight - input.CurrentWeight;
        var theoreticalUsdt = deltaWeight * input.TotalEquity;

        // Step 5: Volatility ratio
        var volRatio = MathUtils.VolatilityRatio(closes, VolRatioShortBars, VolRatioLongBars);

        // Step 6: Wedge filter
        var orderUsdt = ApplyWedgeFilter(theoreticalUsdt, deltaWeight, volRatio, cfg, input.Market.IsQuiet);

        return new Output
        {
            TargetWeight = targetWeight,
            Signal = signal,
            TheoreticalUsdt = theoreticalUsdt,
            OrderUsdt = orderUsdt,
            VolatilityRatio = volRatio
        };
    }

    private static decimal ApplyWedgeFilter(
        decimal theoreticalUsdt, decimal deltaWeight, decimal volRatio,
        Chromosome cfg, bool isQuiet)
    {
        var absOrder = Math.Abs(theoreticalUsdt);
        var sign = theoreticalUsdt >= 0 ? 1m : -1m;

        if (absOrder >= cfg.MinOrderThreshold)
            return theoreticalUsdt; // Direct pass

        if (absOrder == 0) return 0;

        // Wedge zone: (0, MinOrderThreshold)
        if (isQuiet) return 0; // Dust suppression in quiet state

        // Non-quiet: check wedge breakout condition
        bool wedgeBreakout = Math.Abs(deltaWeight) >= cfg.DeltaWeightThreshold
                             || volRatio >= cfg.VolatilityRatioThreshold;

        return wedgeBreakout ? sign * cfg.MinOrderThreshold : 0;
    }
}
