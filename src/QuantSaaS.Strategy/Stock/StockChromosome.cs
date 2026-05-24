using QuantSaaS.Core.Models;

namespace QuantSaaS.Strategy.Stock;

/// <summary>
/// Chromosome for equity (Stock / ETF) strategies.
/// Extends the base chromosome with stock-specific parameters.
/// All fields participate in GA crossover/mutation unless marked [SpawnPoint].
/// </summary>
public class StockChromosome : Chromosome
{
    // ── EMA window parameters ─────────────────────────────────────────────────

    /// <summary>Short EMA look-back in trading bars.</summary>
    public decimal EmaShortBars { get; set; } = 20m;

    /// <summary>Long EMA look-back in trading bars.</summary>
    public decimal EmaLongBars { get; set; } = 50m;

    // ── Risk parameters ───────────────────────────────────────────────────────

    /// <summary>
    /// Maximum fraction of total equity to deploy in a single position.
    /// Complements SpawnPoint.CapitalPolicy.MaxPositionConcentration (the SpawnPoint
    /// sets the epoch-level ceiling; this parameter is the strategy-level target).
    /// </summary>
    public decimal MaxAllocationFraction { get; set; } = 0.8m;

    /// <summary>
    /// Minimum holding period in trading days before a floated position may be fully sold.
    /// Prevents excessive turnover.
    /// </summary>
    public decimal MinHoldingDays { get; set; } = 5m;

    // ── Stock-specific bounds ─────────────────────────────────────────────────

    public new static class Bounds
    {
        public const decimal EmaShortMin = 5m, EmaShortMax = 30m;
        public const decimal EmaLongMin = 20m, EmaLongMax = 200m;
        public const decimal MaxAllocMin = 0.2m, MaxAllocMax = 1.0m;
        public const decimal MinHoldMin = 1m, MinHoldMax = 20m;
    }

    public override StockChromosome Clamp()
    {
        base.Clamp();
        EmaShortBars = Math.Clamp(EmaShortBars, Bounds.EmaShortMin, Bounds.EmaShortMax);
        EmaLongBars = Math.Clamp(EmaLongBars, Bounds.EmaLongMin, Bounds.EmaLongMax);
        MaxAllocationFraction = Math.Clamp(MaxAllocationFraction, Bounds.MaxAllocMin, Bounds.MaxAllocMax);
        MinHoldingDays = Math.Clamp(MinHoldingDays, Bounds.MinHoldMin, Bounds.MinHoldMax);

        // Structural constraint: short EMA must be strictly less than long EMA
        if (EmaShortBars >= EmaLongBars)
            EmaShortBars = Math.Max(Bounds.EmaShortMin, EmaLongBars - 5m);

        return this;
    }

    public static new StockChromosome DefaultSeed => new StockChromosome
    {
        Beta = 1.2m,
        Gamma = 0.3m,
        SigmaFloor = 0.005m,
        CoefX1 = 1.0m,
        CoefX2 = 0.4m,
        CoefX3 = 0.2m,
        DeltaWeightThreshold = 0.04m,
        VolatilityRatioThreshold = 1.6m,
        MinOrderThreshold = 10.1m,
        EmaShortBars = 20m,
        EmaLongBars = 50m,
        MaxAllocationFraction = 0.7m,
        MinHoldingDays = 5m,
    };
}
