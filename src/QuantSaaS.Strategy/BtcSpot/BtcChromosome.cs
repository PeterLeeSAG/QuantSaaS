using QuantSaaS.Core.Models;

namespace QuantSaaS.Strategy.BtcSpot;

/// <summary>
/// BTC spot strategy chromosome.
/// Extends base Chromosome with macro DCA sizing.
/// </summary>
public sealed class BtcChromosome : Chromosome
{
    /// <summary>Nominal quote-currency amount for each macro DCA buy.</summary>
    public decimal MonthlyMacroBuyQuote { get; set; } = 500m;

    private new static class Bounds
    {
        public const decimal MacroBuyMin = 10m, MacroBuyMax = 10000m;
    }

    public override BtcChromosome Clamp()
    {
        base.Clamp();
        MonthlyMacroBuyQuote = Math.Clamp(MonthlyMacroBuyQuote, Bounds.MacroBuyMin, Bounds.MacroBuyMax);
        return this;
    }

    public static new BtcChromosome DefaultSeed => new BtcChromosome
    {
        Beta = 1.0m, Gamma = 0.5m, SigmaFloor = 0.01m,
        CoefX1 = 1.0m, CoefX2 = 0.5m, CoefX3 = 0.3m,
        DeltaWeightThreshold = 0.03m, VolatilityRatioThreshold = 1.8m,
        MinOrderThreshold = 10.1m, MonthlyMacroBuyQuote = 500m,
    };
}
