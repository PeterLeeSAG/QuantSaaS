namespace QuantSaaS.Core.Models;

/// <summary>
/// Base chromosome. Subclassed by strategy-specific implementations.
/// All fields are mutable so GA operators (Mutate, Crossover) can modify in place.
/// Clamp() must be called after every mutation/crossover to enforce bounds.
/// </summary>
/// <summary>
/// Base chromosome for all GA strategies.
/// Subclassed by strategy-specific implementations (e.g., <see cref="QuantSaaS.Strategy.Stock.StockChromosome"/>).
/// Contains parameters shared across all strategies (Sigmoid engine, wedge filter, order sizing).
/// All fields are mutable so GA operators (Mutate, Crossover) can modify them in-place.
/// Call <see cref="Clamp"/> after every mutation or crossover to enforce bounds and structural constraints.
/// </summary>
public class Chromosome
{
    // ── Micro Sigmoid engine ───────────────────────────────────────────────────

    /// <summary>Sigmoid aggressiveness coefficient. Higher = more frequent rebalancing.</summary>
    public decimal Beta { get; set; } = 1.0m;

    /// <summary>Inventory bias coefficient. 0 = pure signal, >0 = mean-reversion force.</summary>
    public decimal Gamma { get; set; } = 0.5m;

    /// <summary>Signal volatility floor (prevents division by near-zero σ).</summary>
    public decimal SigmaFloor { get; set; } = 0.01m;

    // ── Signal synthesis coefficients ─────────────────────────────────────────

    /// <summary>Weight for price-deviation feature X1 (dimensionless).</summary>
    public decimal CoefX1 { get; set; } = 1.0m;

    /// <summary>Weight for momentum feature X2 (dimensionless).</summary>
    public decimal CoefX2 { get; set; } = 0.5m;

    /// <summary>Weight for acceleration/breakout feature X3 (dimensionless).</summary>
    public decimal CoefX3 { get; set; } = 0.3m;

    // ── Wedge filter ──────────────────────────────────────────────────────────

    /// <summary>Minimum |ΔWeight| to trigger a dust order in wedge zone.</summary>
    public decimal DeltaWeightThreshold { get; set; } = 0.03m;

    /// <summary>Minimum VolatilityRatio to trigger a dust order in wedge zone.</summary>
    public decimal VolatilityRatioThreshold { get; set; } = 1.8m;

    // ── Order sizing ──────────────────────────────────────────────────────────

    /// <summary>Minimum order value in quote currency below which orders are discarded.</summary>
    public decimal MinOrderThreshold { get; set; } = 10.1m;

    // ── Static bounds (used by GeneticEngine) ─────────────────────────────────

    public static class Bounds
    {
        public const decimal BetaMin = 0.1m, BetaMax = 5.0m;
        public const decimal GammaMin = 0m, GammaMax = 2.0m;
        public const decimal SigmaFloorMin = 0.001m, SigmaFloorMax = 0.1m;
        public const decimal CoefMin = -2m, CoefMax = 2m;
        public const decimal DeltaWeightMin = 0.01m, DeltaWeightMax = 0.1m;
        public const decimal VolRatioThresholdMin = 1.2m, VolRatioThresholdMax = 2.5m;
        public const decimal MinOrderMin = 5.0m, MinOrderMax = 20.0m;
    }

    /// <summary>
    /// Clamps all fields to hard bounds and enforces structural constraints.
    /// Must be called after every mutation or crossover.
    /// </summary>
    public virtual Chromosome Clamp()
    {
        Beta = Math.Clamp(Beta, Bounds.BetaMin, Bounds.BetaMax);
        Gamma = Math.Clamp(Gamma, Bounds.GammaMin, Bounds.GammaMax);
        SigmaFloor = Math.Clamp(SigmaFloor, Bounds.SigmaFloorMin, Bounds.SigmaFloorMax);
        CoefX1 = Math.Clamp(CoefX1, Bounds.CoefMin, Bounds.CoefMax);
        CoefX2 = Math.Clamp(CoefX2, Bounds.CoefMin, Bounds.CoefMax);
        CoefX3 = Math.Clamp(CoefX3, Bounds.CoefMin, Bounds.CoefMax);
        DeltaWeightThreshold = Math.Clamp(DeltaWeightThreshold, Bounds.DeltaWeightMin, Bounds.DeltaWeightMax);
        VolatilityRatioThreshold = Math.Clamp(VolatilityRatioThreshold, Bounds.VolRatioThresholdMin, Bounds.VolRatioThresholdMax);
        MinOrderThreshold = Math.Clamp(MinOrderThreshold, Bounds.MinOrderMin, Bounds.MinOrderMax);
        return this;
    }

    /// <summary>Default seed chromosome for GA cold-start.</summary>
    public static Chromosome DefaultSeed => new Chromosome().Clamp();
}
