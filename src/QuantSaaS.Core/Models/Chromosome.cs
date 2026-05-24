using System;

namespace QuantSaaS.Core.Models;

/// <summary>
/// Evolvable strategy parameters (chromosome). All fields participate in GA crossover/mutation.
/// Call Clamp() after any mutation or crossover to enforce bounds and structural constraints.
/// </summary>
public class Chromosome
{
    // === Sigmoid micro-engine parameters ===
    /// <summary>Sigmoid aggressiveness: larger = more frequent rebalancing [0.1, 5.0]</summary>
    public decimal Beta { get; set; } = 1.0m;

    /// <summary>Inventory bias coefficient: >0 adds mean-reversion force [0, 2.0]</summary>
    public decimal Gamma { get; set; } = 0.5m;

    /// <summary>Minimum signal standard deviation floor (prevents division by near-zero) [0.001, 0.1]</summary>
    public decimal SigmaFloor { get; set; } = 0.01m;

    // === Signal synthesis coefficients (dimensionless features X1/X2/X3) ===
    /// <summary>Weight for price deviation feature X1 (z-score of price vs EMA) [-2, 2]</summary>
    public decimal CoefX1 { get; set; } = 1.0m;

    /// <summary>Weight for momentum feature X2 (log-return ratio short/long EMA) [-2, 2]</summary>
    public decimal CoefX2 { get; set; } = 0.5m;

    /// <summary>Weight for acceleration feature X3 (change in momentum) [-2, 2]</summary>
    public decimal CoefX3 { get; set; } = 0.3m;

    // === Wedge filtering thresholds ===
    /// <summary>Minimum |DeltaWeight| to trigger wedge breakout order [0.01, 0.1]</summary>
    public decimal DeltaWeightThreshold { get; set; } = 0.03m;

    /// <summary>Volatility ratio threshold for wedge breakout [1.2, 2.5]</summary>
    public decimal VolatilityRatioThreshold { get; set; } = 1.8m;

    // === Order management ===
    /// <summary>Minimum order size in USDT (dust filter) [5.0, 20.0]</summary>
    public decimal MinOrderThreshold { get; set; } = 10.1m;

    // === Macro DCA parameters ===
    /// <summary>Base DCA interval in days [7, 90]</summary>
    public decimal MacroDcaIntervalDays { get; set; } = 30m;

    /// <summary>DCA buy fraction of SpendableUSDT per interval [0.05, 0.5]</summary>
    public decimal MacroDcaBuyFraction { get; set; } = 0.2m;

    /// <summary>Macro acceleration multiplier in Bull state [1.0, 3.0]</summary>
    public decimal MacroBullAccelMultiplier { get; set; } = 1.5m;

    // Hard bounds constants
    public static class Bounds
    {
        public const decimal BetaMin = 0.1m, BetaMax = 5.0m;
        public const decimal GammaMin = 0m, GammaMax = 2.0m;
        public const decimal SigmaFloorMin = 0.001m, SigmaFloorMax = 0.1m;
        public const decimal CoefMin = -2m, CoefMax = 2m;
        public const decimal ThresholdMin = 0.01m, ThresholdMax = 0.1m;
        public const decimal VolRatioThresholdMin = 1.2m, VolRatioThresholdMax = 2.5m;
        public const decimal MinOrderMin = 5.0m, MinOrderMax = 20.0m;
        public const decimal MacroDcaDaysMin = 7m, MacroDcaDaysMax = 90m;
        public const decimal MacroDcaBuyFractionMin = 0.05m, MacroDcaBuyFractionMax = 0.5m;
        public const decimal MacroBullAccelMin = 1.0m, MacroBullAccelMax = 3.0m;
    }

    /// <summary>Default seed chromosome for GA cold-start and fallback</summary>
    public static Chromosome DefaultSeed => new Chromosome
    {
        Beta = 1.0m,
        Gamma = 0.5m,
        SigmaFloor = 0.01m,
        CoefX1 = 1.0m,
        CoefX2 = 0.5m,
        CoefX3 = 0.3m,
        DeltaWeightThreshold = 0.03m,
        VolatilityRatioThreshold = 1.8m,
        MinOrderThreshold = 10.1m,
        MacroDcaIntervalDays = 30m,
        MacroDcaBuyFraction = 0.2m,
        MacroBullAccelMultiplier = 1.5m
    };

    /// <summary>
    /// Clamps all fields to hard bounds and enforces structural constraints.
    /// Must be called after any mutation or crossover operation.
    /// </summary>
    public Chromosome Clamp()
    {
        Beta = Math.Clamp(Beta, Bounds.BetaMin, Bounds.BetaMax);
        Gamma = Math.Clamp(Gamma, Bounds.GammaMin, Bounds.GammaMax);
        SigmaFloor = Math.Clamp(SigmaFloor, Bounds.SigmaFloorMin, Bounds.SigmaFloorMax);
        CoefX1 = Math.Clamp(CoefX1, Bounds.CoefMin, Bounds.CoefMax);
        CoefX2 = Math.Clamp(CoefX2, Bounds.CoefMin, Bounds.CoefMax);
        CoefX3 = Math.Clamp(CoefX3, Bounds.CoefMin, Bounds.CoefMax);
        DeltaWeightThreshold = Math.Clamp(DeltaWeightThreshold, Bounds.ThresholdMin, Bounds.ThresholdMax);
        VolatilityRatioThreshold = Math.Clamp(VolatilityRatioThreshold, Bounds.VolRatioThresholdMin, Bounds.VolRatioThresholdMax);
        MinOrderThreshold = Math.Clamp(MinOrderThreshold, Bounds.MinOrderMin, Bounds.MinOrderMax);
        MacroDcaIntervalDays = Math.Clamp(MacroDcaIntervalDays, Bounds.MacroDcaDaysMin, Bounds.MacroDcaDaysMax);
        MacroDcaBuyFraction = Math.Clamp(MacroDcaBuyFraction, Bounds.MacroDcaBuyFractionMin, Bounds.MacroDcaBuyFractionMax);
        MacroBullAccelMultiplier = Math.Clamp(MacroBullAccelMultiplier, Bounds.MacroBullAccelMin, Bounds.MacroBullAccelMax);
        return this;
    }

    public Chromosome DeepClone() => new Chromosome
    {
        Beta = Beta, Gamma = Gamma, SigmaFloor = SigmaFloor,
        CoefX1 = CoefX1, CoefX2 = CoefX2, CoefX3 = CoefX3,
        DeltaWeightThreshold = DeltaWeightThreshold,
        VolatilityRatioThreshold = VolatilityRatioThreshold,
        MinOrderThreshold = MinOrderThreshold,
        MacroDcaIntervalDays = MacroDcaIntervalDays,
        MacroDcaBuyFraction = MacroDcaBuyFraction,
        MacroBullAccelMultiplier = MacroBullAccelMultiplier
    };
}
